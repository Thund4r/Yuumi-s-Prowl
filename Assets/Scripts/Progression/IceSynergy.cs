using System.Collections.Generic;
using UnityEngine;
using YuumisProwl.BallChain;
using YuumisProwl.Managers;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Blue colour synergy V1 — the Ice Patches loop:
    ///   1. Blue match drops a world-space ice patch at the match centroid.
    ///   2. Balls entering a patch (first contact, or re-entering after the
    ///      re-entry cooldown) accrue +1 freeze stack.
    ///   3. At RunConfig.iceFreezeStackThreshold stacks, the ball is marked
    ///      frozen and its stack counter zeros out.
    ///   4. When a frozen ball is destroyed by any means, BallChainManager fires
    ///      OnFrozenBallDestroyed and IceSynergy spawns an Icicle from that
    ///      position. The icicle locks onto a random ball not currently being
    ///      targeted by another icicle and destroys it on contact.
    ///   5. Chain reactions: icicles can target frozen balls; destroying one
    ///      spawns another icicle.
    ///
    /// Gated by RuntimeStats.IcePatchesEnabled (set by the IcePatches anchor
    /// upgrade). All patches / icicles are torn down when the synergy is
    /// disabled or the scene component is destroyed.
    ///
    /// Setup: add to a GameObject in the Game scene; wire MatchProcessor,
    /// BallChainManager, RuntimeStats, RunConfig. Optionally assign an icePatchPrefab
    /// (Sprite/Quad with a transparent ice disc) and an iciclePrefab (any sprite/mesh)
    /// — both fall back to runtime-created placeholder visuals if left null.
    /// </summary>
    public class IceSynergy : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private RuntimeStats runtimeStats;
        [SerializeField] private RunConfig config;

        [Header("Visuals (optional)")]
        [Tooltip("Prefab spawned at each ice patch site. Should be a 2D-flat visual (sprite, quad, or particle); scale will be set to match patchRadius. If null, a fallback primitive is created at runtime.")]
        [SerializeField] private GameObject icePatchPrefab;
        [Tooltip("Prefab for the homing icicle projectile (must have an Icicle component). If null, a placeholder Icicle GameObject is created at runtime.")]
        [SerializeField] private Icicle iciclePrefab;
        [Tooltip("Initial pool size for ice patches.")]
        [SerializeField, Min(1)] private int initialPatchPoolSize = 8;
        [Tooltip("Initial pool size for icicles.")]
        [SerializeField, Min(1)] private int initialIciclePoolSize = 8;

        // ----- active patches -----
        private readonly List<IcePatch> activePatches = new List<IcePatch>(16);

        // ----- icicle pool / targeting -----
        private readonly Queue<Icicle> iciclePool = new Queue<Icicle>(16);
        private readonly List<Icicle> activeIcicles = new List<Icicle>(16);
        private readonly HashSet<Ball> targetedBalls = new HashSet<Ball>();

        // ----- ice patch visual pool -----
        private readonly Queue<GameObject> patchVisualPool = new Queue<GameObject>(16);

        // ----- chain slowdown state (BlueChainSlowdown / BlueSlowdownDuration) -----
        private float slowdownExpiryTime;
        private bool slowdownActive;

        // Frame on which any cryo burst was last scheduled (match-triggered or cascade).
        // Used to dedup subsequent cascade bursts in the same frame, so a single
        // destruction event that kills multiple frozen balls produces one burst, not N.
        private int lastBurstFrame = -1;

        private void Awake()
        {
            for (int i = 0; i < initialIciclePoolSize; i++)
                iciclePool.Enqueue(CreateIcicleInstance());

            for (int i = 0; i < initialPatchPoolSize; i++)
                patchVisualPool.Enqueue(CreatePatchVisualInstance());
        }

        private void OnEnable()
        {
            if (matchProcessor != null)
                matchProcessor.OnMatchVisual += HandleMatchVisual;
            if (ballChainManager != null)
                ballChainManager.OnFrozenBallDestroyed += HandleFrozenBallDestroyed;
            if (gameManager != null)
            {
                gameManager.OnGameWon += HandleRoundEnded;
                gameManager.OnGameLost += HandleRoundEnded;
                gameManager.OnWaveCleared += HandleRoundEnded;
            }
        }

        private void OnDisable()
        {
            if (matchProcessor != null)
                matchProcessor.OnMatchVisual -= HandleMatchVisual;
            if (ballChainManager != null)
                ballChainManager.OnFrozenBallDestroyed -= HandleFrozenBallDestroyed;

            // Clear active state so a re-enable starts clean.
            TearDownAllPatches();
            ReleaseAllIcicles();
            targetedBalls.Clear();
            ClearSlowdown();
            if (gameManager != null)
            {
                gameManager.OnGameWon -= HandleRoundEnded;
                gameManager.OnGameLost -= HandleRoundEnded;
                gameManager.OnWaveCleared -= HandleRoundEnded;
            }
        }

        private void Update()
        {
            if (runtimeStats == null || !runtimeStats.IcePatchesEnabled)
            {
                // Synergy off — make sure nothing lingers.
                if (activePatches.Count > 0) TearDownAllPatches();
                if (slowdownActive) ClearSlowdown();
                return;
            }

            TickPatches();
            TickSlowdown();
        }

        // ============================================================
        // Match → spawn ice patch
        // ============================================================

        private void HandleMatchVisual(List<Vector3> positions, BallColor color, int cascadeIndex)
        {
            if (runtimeStats == null || !runtimeStats.IcePatchesEnabled) return;
            if (color != BallColor.Blue) return;
            if (positions == null || positions.Count == 0) return;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < positions.Count; i++) centroid += positions[i];
            centroid /= positions.Count;

            float radius = config != null ? config.icePatchRadius : 2.5f;
            float duration = config != null ? config.icePatchDuration : 5f;
            float reentryCooldown = config != null ? config.icePatchReentryCooldown : 2f;

            SpawnPatch(centroid, radius, duration, reentryCooldown);

            if (runtimeStats.CryoBurstEnabled)
            {
                lastBurstFrame = Time.frameCount;
                StartCoroutine(CryoBurstNextFrame(centroid));
            }

            if (runtimeStats.BlueChainSlowdownEnabled)
                ApplyChainSlowdown();
        }

        private void HandleRoundEnded()
        {
            // Clear all state immediately on round end — no lingering patches or icicles in the victory / defeat screen.
            TearDownAllPatches();
            ReleaseAllIcicles();
            targetedBalls.Clear();
            ClearSlowdown();
        }

        // ============================================================
        // Chain slowdown (BlueChainSlowdown / BlueSlowdownDuration)
        // ============================================================

        private void ApplyChainSlowdown()
        {
            if (ballChainManager == null || config == null) return;

            int blueCount = runtimeStats != null ? runtimeStats.GetColorSynergyCount(BallColor.Blue) : 0;
            float reduction = blueCount * config.blueSlowdownPerUpgrade;
            float multiplier = Mathf.Max(config.blueSlowdownMinMultiplier, 1f - reduction);

            float duration = config.blueSlowdownBaseDuration
                             + (runtimeStats != null ? runtimeStats.BlueSlowdownDurationBonus : 0f);

            // Refresh the timer rather than stacking durations — multiple blue matches in
            // quick succession keep the chain slow but don't compound into a longer total.
            ballChainManager.SetChainSpeedMultiplier(multiplier);
            slowdownExpiryTime = Time.time + duration;
            slowdownActive = true;
        }

        private void TickSlowdown()
        {
            if (!slowdownActive) return;
            if (Time.time < slowdownExpiryTime) return;

            if (ballChainManager != null) ballChainManager.SetChainSpeedMultiplier(1f);
            slowdownActive = false;
        }

        private void ClearSlowdown()
        {
            if (ballChainManager != null) ballChainManager.SetChainSpeedMultiplier(1f);
            slowdownActive = false;
            slowdownExpiryTime = 0f;
        }

        // ============================================================
        // Cryo Burst — expanding AoE that stacks frost on contact
        // ============================================================

        private System.Collections.IEnumerator CryoBurstNextFrame(Vector3 center)
        {
            yield return null;
            yield return StartCoroutine(CryoBurstRoutine(center));
        }

        private System.Collections.IEnumerator CryoBurstRoutine(Vector3 center)
        {
            float maxRadius = config != null ? config.cryoBurstRadius : 2f;
            float duration  = config != null ? config.cryoBurstDuration : 0.4f;

            GameObject ring = AcquirePatchVisual();
            if (ring != null)
            {
                ring.transform.position = center;
                ring.transform.localScale = Vector3.zero;
                ring.SetActive(true);
            }

            var alreadyStacked = new HashSet<Ball>();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float currentRadius = Mathf.Lerp(0f, maxRadius, t);

                if (ring != null)
                {
                    float diameter = currentRadius * 2f;
                    ring.transform.localScale = new Vector3(diameter, diameter, 1f);
                }

                ApplyBurstHits(center, currentRadius, alreadyStacked);
                yield return null;
            }

            // Final sweep at max radius to catch anything the lerp skipped on the last frame.
            ApplyBurstHits(center, maxRadius, alreadyStacked);

            ReleasePatchVisual(ring);
        }

        private void ApplyBurstHits(Vector3 center, float radius, HashSet<Ball> alreadyStacked)
        {
            if (ballChainManager == null || radius <= 0f) return;

            int threshold = GetEffectiveFreezeThreshold();
            var segments = ballChainManager.GetSegments();

            Collider[] hits = Physics.OverlapSphere(center, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i].CompareTag("Ball")) continue;
                Ball ball = hits[i].GetComponent<Ball>();
                if (ball == null || !ball.gameObject.activeInHierarchy) continue;
                if (alreadyStacked.Contains(ball)) continue;

                BallNode node = FindNodeFor(ball, segments);
                if (node == null || node.isFrozen) continue;

                node.freezeStacks++;
                ball.SetFrostStacks(node.freezeStacks);
                if (node.freezeStacks >= threshold)
                {
                    node.isFrozen = true;
                    node.freezeStacks = 0;
                    ball.SetFrozen(true);
                }
                ball.FlashFrost();
                alreadyStacked.Add(ball);
            }
        }

        private BallNode FindNodeFor(Ball ball, List<ChainSegment> segments)
        {
            if (segments == null) return null;
            for (int s = 0; s < segments.Count; s++)
            {
                var seg = segments[s];
                for (int i = 0; i < seg.balls.Count; i++)
                {
                    if (seg.balls[i].ball == ball) return seg.balls[i];
                }
            }
            return null;
        }

        private int GetEffectiveFreezeThreshold()
        {
            int baseThreshold = config != null ? config.iceFreezeStackThreshold : 3;
            int reduction = runtimeStats != null ? runtimeStats.FrostThresholdReduction : 0;
            return Mathf.Max(1, baseThreshold - reduction);
        }

        private void SpawnPatch(Vector3 center, float radius, float duration, float reentryCooldown)
        {
            GameObject visual = AcquirePatchVisual();
            if (visual != null)
            {
                visual.transform.position = new Vector3(center.x, center.y, center.z);
                // Diameter scaling — visual is assumed to be a unit sprite/quad.
                visual.transform.localScale = new Vector3(radius, radius, 1f);
                visual.SetActive(true);
            }

            var patch = new IcePatch
            {
                center = center,
                radius = radius,
                expiryTime = Time.time + duration,
                reentryCooldown = reentryCooldown,
                visual = visual,
            };
            activePatches.Add(patch);
        }

        // ============================================================
        // Per-frame patch ticking — stack management
        // ============================================================

        private void TickPatches()
        {
            if (ballChainManager == null) return;

            int threshold = GetEffectiveFreezeThreshold();
            float now = Time.time;

            // Iterate patches reverse so we can remove expired ones inline.
            for (int p = activePatches.Count - 1; p >= 0; p--)
            {
                var patch = activePatches[p];

                if (now >= patch.expiryTime)
                {
                    ReleasePatchVisual(patch.visual);
                    activePatches.RemoveAt(p);
                    continue;
                }

                // Walk every ball and update its state vs this patch.
                var allSegments = ballChainManager.GetSegments();
                if (allSegments == null) continue;
                float r2 = patch.radius * patch.radius;

                for (int s = 0; s < allSegments.Count; s++)
                {
                    var seg = allSegments[s];
                    for (int i = 0; i < seg.balls.Count; i++)
                    {
                        var node = seg.balls[i];
                        if (node == null || node.ball == null) continue;
                        // Frozen balls don't accrue more stacks (stacks consumed at freeze).
                        if (node.isFrozen) continue;

                        Vector3 ballPos = node.ball.transform.position;
                        float dx = ballPos.x - patch.center.x;
                        float dy = ballPos.y - patch.center.y;
                        bool inside = (dx * dx + dy * dy) <= r2;

                        patch.perBallState.TryGetValue(node, out var state);
                        // Default state: not previously seen by this patch.

                        if (inside)
                        {
                            // Ball is inside this frame.
                            if (!state.everInside)
                            {
                                // First contact — apply stack.
                                ApplyStack(node, threshold);
                                state.everInside = true;
                                state.insideNow = true;
                                state.lastExitTime = 0f;
                                patch.perBallState[node] = state;
                            }
                            else if (!state.insideNow)
                            {
                                // Re-entry after being outside. Only re-stack if cooldown elapsed.
                                if (now - state.lastExitTime >= patch.reentryCooldown)
                                    ApplyStack(node, threshold);
                                state.insideNow = true;
                                patch.perBallState[node] = state;
                            }
                            // else: still inside, no-op.
                        }
                        else
                        {
                            // Ball not inside.
                            if (state.insideNow)
                            {
                                state.insideNow = false;
                                state.lastExitTime = now;
                                patch.perBallState[node] = state;
                            }
                        }
                    }
                }
            }
        }

        private void ApplyStack(BallNode node, int threshold)
        {
            if (node.isFrozen) return;
            node.freezeStacks++;
            if (node.ball != null) node.ball.SetFrostStacks(node.freezeStacks);

            if (node.freezeStacks >= threshold)
            {
                node.isFrozen = true;
                node.freezeStacks = 0;
                if (node.ball != null) node.ball.SetFrozen(true);
            }
        }

        // ============================================================
        // Frozen ball destroyed → spawn icicle
        // ============================================================

        private void HandleFrozenBallDestroyed(Vector3 worldPos, int power)
        {
            if (runtimeStats == null || !runtimeStats.IcePatchesEnabled) return;
            if (ballChainManager == null) return;

            // Shatter Cascade extends CryoBurst — destroyed frozen balls also emit a burst.
            // Capped at one burst per frame so a single destruction event (a match, a bomb,
            // a pierce, a red explosion, etc.) that kills multiple frozen balls fires one
            // burst total, not one per ball. Chain reactions across frames still propagate.
            if (runtimeStats.CryoBurstChainEnabled
                && runtimeStats.CryoBurstEnabled
                && Time.frameCount != lastBurstFrame)
            {
                lastBurstFrame = Time.frameCount;
                StartCoroutine(CryoBurstNextFrame(worldPos));
            }

            // Frost overcharge: a higher-power frozen ball spawns more icicles (one per power).
            int icicles = Mathf.Max(1, power);
            float speed = config != null ? config.icicleSpeed : 8f;
            float arrival = config != null ? config.icicleArrivalDistance : 0.4f;

            for (int n = 0; n < icicles; n++)
            {
                Ball target = PickRandomUntargetedBall();
                if (target == null) break;   // no more eligible targets

                Icicle icicle = AcquireIcicle();
                if (icicle == null) break;

                targetedBalls.Add(target);
                activeIcicles.Add(icicle);
                icicle.Launch(worldPos, target, speed, arrival, ballChainManager, matchProcessor, this);
            }
        }

        /// <summary>Called by Icicle when it resolves (hit, target lost, etc.).</summary>
        public void ReleaseIcicle(Icicle icicle)
        {
            if (icicle == null) return;
            if (icicle.Target != null) targetedBalls.Remove(icicle.Target);
            activeIcicles.Remove(icicle);
            icicle.HardReset();
            iciclePool.Enqueue(icicle);
        }

        private Ball PickRandomUntargetedBall()
        {
            var allSegments = ballChainManager.GetSegments();
            if (allSegments == null) return null;

            bool huntFrozen = runtimeStats != null && runtimeStats.FreezeTheHuntedEnabled;
            List<Ball> frozenEligible = huntFrozen ? new List<Ball>(8) : null;
            List<Ball> eligible = new List<Ball>(32);

            for (int s = 0; s < allSegments.Count; s++)
            {
                var seg = allSegments[s];
                for (int i = 0; i < seg.balls.Count; i++)
                {
                    var node = seg.balls[i];
                    if (node == null || node.ball == null) continue;
                    if (!node.ball.gameObject.activeInHierarchy) continue;
                    if (targetedBalls.Contains(node.ball)) continue;
                    eligible.Add(node.ball);
                    if (huntFrozen && node.isFrozen) frozenEligible.Add(node.ball);
                }
            }

            // Freeze the Hunted: if any untargeted frozen balls exist, pick from those.
            if (frozenEligible != null && frozenEligible.Count > 0)
                return frozenEligible[Random.Range(0, frozenEligible.Count)];

            if (eligible.Count == 0) return null;
            return eligible[Random.Range(0, eligible.Count)];
        }

        // ============================================================
        // Pooling helpers
        // ============================================================

        private Icicle AcquireIcicle()
        {
            Icicle ic = iciclePool.Count > 0 ? iciclePool.Dequeue() : CreateIcicleInstance();
            return ic;
        }

        private Icicle CreateIcicleInstance()
        {
            Icicle ic;
            if (iciclePrefab != null)
            {
                ic = Instantiate(iciclePrefab, transform);
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform);
                go.transform.localScale = new Vector3(0.25f, 0.5f, 0.25f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                ic = go.AddComponent<Icicle>();
                go.name = "Icicle_Placeholder";
            }
            ic.gameObject.SetActive(false);
            return ic;
        }

        private GameObject AcquirePatchVisual()
        {
            if (patchVisualPool.Count > 0) return patchVisualPool.Dequeue();
            return CreatePatchVisualInstance();
        }

        private void ReleasePatchVisual(GameObject visual)
        {
            if (visual == null) return;
            visual.SetActive(false);
            patchVisualPool.Enqueue(visual);
        }

        private GameObject CreatePatchVisualInstance()
        {
            GameObject visual;
            if (icePatchPrefab != null)
            {
                visual = Instantiate(icePatchPrefab, transform);
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.transform.SetParent(transform);
                var col = visual.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = visual.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    // Translucent ice-blue placeholder.
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    mat.color = new Color(0.6f, 0.85f, 1f, 0.35f);
                    rend.material = mat;
                }
                visual.name = "IcePatch_Placeholder";
            }
            visual.SetActive(false);
            return visual;
        }

        // ============================================================
        // Teardown
        // ============================================================

        private void TearDownAllPatches()
        {
            for (int i = activePatches.Count - 1; i >= 0; i--)
                ReleasePatchVisual(activePatches[i].visual);
            activePatches.Clear();
        }

        private void ReleaseAllIcicles()
        {
            // Iterate a snapshot — ReleaseIcicle mutates activeIcicles.
            var snapshot = activeIcicles.ToArray();
            for (int i = 0; i < snapshot.Length; i++) ReleaseIcicle(snapshot[i]);
        }
    }

    /// <summary>
    /// Plain data record for an active ice patch. Lives in IceSynergy.activePatches; the
    /// matching GameObject visual lives in IceSynergy's visual pool.
    /// </summary>
    internal class IcePatch
    {
        public Vector3 center;
        public float radius;
        public float expiryTime;
        public float reentryCooldown;
        public GameObject visual;
        public readonly Dictionary<BallNode, IcePatchBallState> perBallState
            = new Dictionary<BallNode, IcePatchBallState>(16);
    }

    /// <summary>Per-ball state inside a single ice patch.</summary>
    internal struct IcePatchBallState
    {
        /// <summary>True after the first time this ball entered the patch.</summary>
        public bool everInside;
        /// <summary>True while the ball is currently inside the patch.</summary>
        public bool insideNow;
        /// <summary>Time.time when this ball most recently left the patch.</summary>
        public float lastExitTime;
    }
}
