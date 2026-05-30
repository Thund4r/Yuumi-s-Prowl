using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YuumisProwl.BallChain;
using YuumisProwl.Utilities;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Orange colour synergy — the Conductor / arc catalyst:
    ///   1. An orange match fires a bouncing arc (chain-lightning) from the match centroid.
    ///   2. The arc hops ball-to-ball (animated, one hop at a time), charging each by ITS colour:
    ///      blue → frost stack, purple → rage, red → ignite stack; a ball whose colour has no
    ///      active synergy gets a weak "static" stack instead. The first hop seeks the nearest
    ///      ball to the match; later hops pick a random ball within arcHopChainRange chain
    ///      positions (front or back) of the current ball.
    ///   3. A red ball that reaches the ignite threshold becomes primed; a primed ball leaves a
    ///      mini-explosion when destroyed (OnIgnitedBallDestroyed → coalesced ExplodeMini).
    ///   4. A ball that reaches the static threshold pops (weak single removal).
    ///
    /// Orange is a pure catalyst — it never fires another colour's gated payoff, only charges the
    /// input resource — so it is weak alone and strong only in combination. Gated by
    /// RuntimeStats.ConductorEnabled (the Conductor anchor upgrade).
    ///
    /// Setup: add to a GameObject in the Game scene; wire MatchProcessor, BallChainManager,
    /// RuntimeStats, RunConfig, and (optionally) RageMeter for purple charging.
    /// </summary>
    public class ArcSynergy : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private RuntimeStats runtimeStats;
        [SerializeField] private RunConfig config;
        [Tooltip("Optional — charged when an arc hops onto a purple ball (rage). If null, purple hops fall back to static.")]
        [SerializeField] private RageMeter rageMeter;

        [Header("Arc Behaviour")]
        [Tooltip("When hopping, the arc jumps to a RANDOM ball within this many chain positions in front of OR behind the ball it is currently at.")]
        [SerializeField, Min(1)] private int arcHopChainRange = 3;
        [Tooltip("Seconds the bolt takes to travel each hop before that ball's charge is applied.")]
        [SerializeField, Min(0f)] private float arcTravelDuration = 0.06f;
        [Tooltip("Seconds of pause between one hop landing and the next hop launching.")]
        [SerializeField, Min(0f)] private float arcHopDelay = 0.08f;

        [Header("Arc Visual")]
        [SerializeField] private Color arcColor = new Color(0.6f, 0.95f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float arcWidth = 0.1f;
        [SerializeField, Min(0.02f)] private float arcFlashDuration = 0.12f;
        [Tooltip("How far each hop bows out to the side, as a fraction of the hop length. 0 = straight line.")]
        [SerializeField, Range(0f, 3f)] private float arcBowFactor = 0.25f;
        [Tooltip("How far the bolt stops short of each ball centre (~ball radius) so endpoints sit on the surface, not buried inside.")]
        [SerializeField, Min(0f)] private float arcEndInset = 0.35f;
        [Tooltip("Curve samples per hop — higher = smoother arc.")]
        [SerializeField, Range(2, 24)] private int arcCurveResolution = 8;

        // Borrow-pool of LineRenderers — each hop borrows one bolt and the whole arc releases them
        // when it finishes, so concurrent arcs (multi-fire rage) don't fight over the same visuals.
        private readonly Queue<LineRenderer> linePool = new Queue<LineRenderer>();
        private readonly List<LineRenderer> allLines = new List<LineRenderer>(8);
        private readonly List<Vector3> arcPathBuffer = new List<Vector3>(32);
        private readonly List<BallNode> candidateBuffer = new List<BallNode>(16);
        private FrameCoalescer igniteCoalescer;
        private int arcsFired;

        private void Awake()
        {
            float mergeRadius = config != null ? config.igniteMiniRadius : 1f;
            igniteCoalescer = new FrameCoalescer(this, mergeRadius, (center, count) => ExplodeMini(center));
        }

        private void OnEnable()
        {
            if (matchProcessor != null)
                matchProcessor.OnMatchVisual += HandleMatchVisual;
            if (ballChainManager != null)
                ballChainManager.OnIgnitedBallDestroyed += HandleIgnitedBallDestroyed;
        }

        private void OnDisable()
        {
            if (matchProcessor != null)
                matchProcessor.OnMatchVisual -= HandleMatchVisual;
            if (ballChainManager != null)
                ballChainManager.OnIgnitedBallDestroyed -= HandleIgnitedBallDestroyed;

            igniteCoalescer?.Clear();
            for (int i = 0; i < allLines.Count; i++)
                if (allLines[i] != null) allLines[i].enabled = false;
        }

        // ============================================================
        // Orange match → animated bouncing arc
        // ============================================================

        private void HandleMatchVisual(List<Vector3> positions, BallColor color, int cascadeIndex)
        {
            if (runtimeStats == null || !runtimeStats.ConductorEnabled) return;
            if (color != BallColor.Orange) return;
            if (positions == null || positions.Count == 0) return;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < positions.Count; i++) centroid += positions[i];
            centroid /= positions.Count;

            StartCoroutine(FireArc(centroid));
        }

        /// <summary>
        /// Animated chain-lightning. Defers one frame (the match is still being processed), then
        /// hops ball-to-ball: travel the bolt, THEN apply that ball's charge, pause, repeat. The
        /// first hop seeks the nearest ball to the match centroid; every hop after picks a random
        /// ball within arcHopChainRange chain positions (front or back) of the current ball.
        /// </summary>
        private IEnumerator FireArc(Vector3 start)
        {
            // OnMatchVisual fires mid-match-processing (before RemoveBalls); wait a frame so
            // charging / static-popping can't mutate the chain re-entrantly.
            yield return null;

            if (ballChainManager == null || runtimeStats == null) yield break;
            int bounces = runtimeStats.ArcBounces;
            if (bounces <= 0) yield break;

            float firstHopRange = config != null ? config.arcRange : 3f;

            // Charge units applied to COLOUR synergies (never to static). Supercharge doubles it
            // every Nth arc.
            arcsFired++;
            int units = 1 + Mathf.Max(0, runtimeStats.ArcResonanceBonus);
            int superN = config != null ? config.superchargeEveryNth : 3;
            if (runtimeStats.SuperchargeEnabled && superN > 0 && arcsFired % superN == 0)
                units *= 2;

            var visited = new HashSet<Ball>();
            var borrowed = new List<LineRenderer>(bounces);

            Vector3 current = start;
            Ball currentBall = null;

            for (int b = 0; b < bounces; b++)
            {
                BallNode node = (currentBall == null)
                    ? FindNearestHop(current, firstHopRange, visited)
                    : PickHopWithinChainRange(currentBall, arcHopChainRange, visited);
                if (node == null) break;             // nothing reachable — arc fizzles

                Vector3 target = node.ball.transform.position;
                visited.Add(node.ball);

                // Travel the bolt to the ball; its charge applies only once the bolt arrives.
                LineRenderer lr = AcquireLine();
                borrowed.Add(lr);
                yield return TravelBolt(current, target, b, lr);

                node.ball.FlashZap();
                ChargeBall(node, units);          // may pop the ball (static)

                current = target;                 // captured before the charge (a popped ball pools to origin)
                currentBall = node.ball;          // if it popped, the next PickHop won't find it → arc ends

                if (arcHopDelay > 0f) yield return new WaitForSeconds(arcHopDelay);
            }

            // Hold the completed chain briefly, then release every bolt back to the pool.
            yield return new WaitForSeconds(arcFlashDuration);
            for (int i = 0; i < borrowed.Count; i++) ReleaseLine(borrowed[i]);
        }

        /// <summary>Nearest active, non-power-up, not-yet-visited ball within range of `from` (first hop).</summary>
        private BallNode FindNearestHop(Vector3 from, float range, HashSet<Ball> visited)
        {
            var segments = ballChainManager.GetSegments();
            if (segments == null) return null;

            float r2 = range * range;
            BallNode best = null;
            float bestDist = float.MaxValue;

            for (int s = 0; s < segments.Count; s++)
            {
                var seg = segments[s];
                for (int i = 0; i < seg.balls.Count; i++)
                {
                    var node = seg.balls[i];
                    if (node?.ball == null) continue;
                    if (!node.ball.gameObject.activeInHierarchy) continue;
                    if (node.ball.PowerUpType != BallPowerUpType.None) continue;
                    if (visited.Contains(node.ball)) continue;

                    float d = (node.ball.transform.position - from).sqrMagnitude;
                    if (d <= r2 && d < bestDist)
                    {
                        bestDist = d;
                        best = node;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// Picks a random active, non-power-up, not-yet-visited ball within chainRange chain
        /// positions (front or back) of fromBall. Returns null if fromBall is gone or no
        /// candidate qualifies.
        /// </summary>
        private BallNode PickHopWithinChainRange(Ball fromBall, int chainRange, HashSet<Ball> visited)
        {
            var flat = ballChainManager.GetBallChain();   // front-to-back
            if (flat == null || flat.Count == 0) return null;

            int curIdx = -1;
            for (int i = 0; i < flat.Count; i++)
                if (flat[i].ball == fromBall) { curIdx = i; break; }
            if (curIdx < 0) return null;                  // current ball gone (e.g. it popped)

            int lo = Mathf.Max(0, curIdx - chainRange);
            int hi = Mathf.Min(flat.Count - 1, curIdx + chainRange);

            candidateBuffer.Clear();
            for (int i = lo; i <= hi; i++)
            {
                if (i == curIdx) continue;
                var node = flat[i];
                if (node?.ball == null) continue;
                if (!node.ball.gameObject.activeInHierarchy) continue;
                if (node.ball.PowerUpType != BallPowerUpType.None) continue;
                if (visited.Contains(node.ball)) continue;
                candidateBuffer.Add(node);
            }

            if (candidateBuffer.Count == 0) return null;
            return candidateBuffer[Random.Range(0, candidateBuffer.Count)];
        }

        // ============================================================
        // Colour-matched charging
        // ============================================================

        private void ChargeBall(BallNode node, int units)
        {
            BallColor c = node.ball.BallColor;
            if (c == BallColor.Blue && runtimeStats.IcePatchesEnabled)
                ChargeFrost(node, units);
            else if (c == BallColor.Red && runtimeStats.RedMatchExplosionEnabled)
                ChargeIgnite(node, units);
            else if (c == BallColor.Purple && runtimeStats.RageEnabled && rageMeter != null)
                rageMeter.AddRage((config != null ? config.arcRageGain : 5f) * units);
            else
                ChargeStatic(node);
        }

        private void ChargeFrost(BallNode node, int units)
        {
            if (node.isFrozen) return;

            int bonus = (runtimeStats.OverloadEnabled && node.freezeStacks > 0) ? 1 : 0;
            node.freezeStacks += units + bonus;
            node.ball.SetFrostStacks(node.freezeStacks);
            if (node.freezeStacks >= EffectiveFreezeThreshold())
            {
                node.isFrozen = true;
                node.freezeStacks = 0;
                node.ball.SetFrozen(true);
            }
        }

        private void ChargeIgnite(BallNode node, int units)
        {
            if (node.primed) return;

            int bonus = (runtimeStats.OverloadEnabled && node.igniteStacks > 0) ? 1 : 0;
            node.igniteStacks += units + bonus;
            node.ball.SetIgniteStacks(node.igniteStacks);
            int threshold = config != null ? config.igniteThreshold : 3;
            if (node.igniteStacks >= threshold)
            {
                node.primed = true;
                node.ball.SetPrimed(true);
            }
        }

        private void ChargeStatic(BallNode node)
        {
            // Baseline only — fixed +1 per hop, never amplified by Resonance / Supercharge /
            // Overload (Orange must not be a standalone damage source).
            node.staticStacks++;
            int threshold = config != null ? config.staticThreshold : 3;
            if (node.staticStacks >= threshold)
            {
                node.staticStacks = 0;
                ballChainManager.RemoveBallAtIndex(node.ball.ChainIndex);
                if (matchProcessor != null)
                    matchProcessor.ProcessPierceAftermath(1);
            }
        }

        private int EffectiveFreezeThreshold()
        {
            int baseT = config != null ? config.iceFreezeStackThreshold : 3;
            int reduction = runtimeStats != null ? runtimeStats.FrostThresholdReduction : 0;
            return Mathf.Max(1, baseT - reduction);
        }

        // ============================================================
        // Ignite mini-explosion (primed red destroyed)
        // ============================================================

        private void HandleIgnitedBallDestroyed(Vector3 worldPos)
        {
            if (runtimeStats == null || !runtimeStats.ConductorEnabled) return;
            igniteCoalescer.Add(worldPos);
        }

        /// <summary>Small AoE removal at a primed-red death site. Mirrors ExplosionSynergy.</summary>
        private void ExplodeMini(Vector3 center)
        {
            if (ballChainManager == null) return;
            float radius = config != null ? config.igniteMiniRadius : 1f;

            Collider[] hits = Physics.OverlapSphere(center, radius);
            var indices = new List<int>();
            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i].CompareTag("Ball")) continue;
                Ball ball = hits[i].GetComponent<Ball>();
                if (ball != null && !indices.Contains(ball.ChainIndex))
                    indices.Add(ball.ChainIndex);
            }
            if (indices.Count == 0) return;

            indices.Sort((a, b) => b.CompareTo(a));   // high → low
            for (int i = 0; i < indices.Count; i++)
                ballChainManager.RemoveBallAtIndex(indices[i]);

            if (matchProcessor != null)
                matchProcessor.ProcessPierceAftermath(indices.Count);
        }

        // ============================================================
        // Arc visual — animated bolt + curved geometry + line pool
        // ============================================================

        /// <summary>
        /// Draws one hop's bolt, growing it from the source toward the target over
        /// arcTravelDuration so the lightning visibly travels before the charge lands.
        /// </summary>
        private IEnumerator TravelBolt(Vector3 a, Vector3 b, int hopIndex, LineRenderer lr)
        {
            if (lr == null) yield break;
            if (!BuildHopCurve(a, b, hopIndex)) { lr.enabled = false; yield break; }

            // Copy the curve — arcPathBuffer is reused by the next hop.
            var pts = new List<Vector3>(arcPathBuffer);
            int total = pts.Count;
            lr.enabled = true;

            if (arcTravelDuration <= 0f)
            {
                lr.positionCount = total;
                for (int i = 0; i < total; i++) lr.SetPosition(i, pts[i]);
                yield break;
            }

            float t = 0f;
            while (t < arcTravelDuration)
            {
                t += Time.deltaTime;
                float frac = Mathf.Clamp01(t / arcTravelDuration);
                int reveal = Mathf.Max(2, Mathf.CeilToInt(frac * total));
                lr.positionCount = reveal;
                for (int i = 0; i < reveal; i++) lr.SetPosition(i, pts[i]);
                yield return null;
            }

            lr.positionCount = total;
            for (int i = 0; i < total; i++) lr.SetPosition(i, pts[i]);
        }

        /// <summary>
        /// Fills arcPathBuffer with a curved bolt for one hop: endpoints inset to the ball
        /// surfaces (so it doesn't bury into the centres) and bowed out to one side, alternating
        /// per hop for a chain-lightning zig-zag. Returns false for a degenerate (zero-length) hop.
        /// </summary>
        private bool BuildHopCurve(Vector3 a, Vector3 b, int hopIndex)
        {
            arcPathBuffer.Clear();
            Vector3 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.0001f) return false;

            Vector3 dir = delta / len;
            float inset = Mathf.Min(arcEndInset, len * 0.4f);   // never collapse a short hop
            Vector3 a2 = a + dir * inset;
            Vector3 b2 = b - dir * inset;

            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            float sign = (hopIndex % 2 == 0) ? 1f : -1f;
            Vector3 control = (a2 + b2) * 0.5f + perp * (len * arcBowFactor) * sign;

            int samples = Mathf.Max(2, arcCurveResolution);
            for (int s = 0; s <= samples; s++)
            {
                float t = (float)s / samples;
                arcPathBuffer.Add(QuadBezier(a2, control, b2, t));
            }
            return true;
        }

        private LineRenderer AcquireLine()
        {
            LineRenderer lr = linePool.Count > 0 ? linePool.Dequeue() : CreateLine();
            lr.enabled = true;
            return lr;
        }

        private void ReleaseLine(LineRenderer lr)
        {
            if (lr == null) return;
            lr.enabled = false;
            lr.positionCount = 0;
            linePool.Enqueue(lr);
        }

        private LineRenderer CreateLine()
        {
            var go = new GameObject("ArcBolt");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = arcColor;
            lr.endColor = arcColor;
            lr.startWidth = arcWidth;
            lr.endWidth = arcWidth;
            lr.numCapVertices = 2;
            lr.enabled = false;
            allLines.Add(lr);
            return lr;
        }

        private static Vector3 QuadBezier(Vector3 a, Vector3 c, Vector3 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }
    }
}
