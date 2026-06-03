using UnityEngine;
using System.Collections.Generic;
using YuumisProwl;
using YuumisProwl.Utilities;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Manages the ball chain along the path. Internally the chain is one or more
    /// ChainSegments — contiguous runs of balls. Gaps between segments (e.g. from a
    /// Bomb explosion) persist; each segment moves, recoils, and matches independently.
    /// Adjacent segments merge automatically when they touch.
    ///
    /// Public API exposes both a flat-chain view (GetBallChain, BallCount, ChainIndex)
    /// for legacy callers, and a segment view (GetSegments, GetSegmentForBall) for
    /// segment-aware logic.
    /// </summary>
    public class BallChainManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PathController pathController;
        [SerializeField] private Ball ballPrefab;

        [Header("Movement Settings")]
        [SerializeField] private float ballSpeed = 2f;
        [SerializeField] private float ballSpacing = 0.5f;

        [Header("Gap Close Settings")]
        [Tooltip("Initial backward speed of the front segment when a gap first opens.")]
        [SerializeField] private float gapCloseSpeed = 2f;
        [Tooltip("Maximum backward speed the front segment can reach while a gap is open.")]
        [SerializeField] private float gapCloseMaxSpeed = 6f;
        [Tooltip("Acceleration applied to the front segment's backward speed while a gap is open, in units/sec^2. 0 = constant speed.")]
        [SerializeField] private float gapCloseAcceleration = 4f;

        private float currentGapCloseSpeed;

        [Header("Insertion Animation")]
        [Tooltip("Seconds taken for an inserted ball to ease into place (and for the chain in front of it to slide forward by one ball spacing).")]
        [SerializeField] private float insertionDuration = 0.15f;

        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 50;

        [Header("Spawn Hole Settings")]
        [SerializeField] private float holeProgress = 0f;
        [Tooltip("Path-progress floor where new balls spawn. Set below holeProgress so the chain extends past the hole as an invisible queue — back-end destructions then produce real gaps the multi-segment system closes naturally. ~-0.25 works for most maps; tune by playtest.")]
        [SerializeField] private float spawnProgress = -0.25f;

        // Events
        public System.Action OnBallReachedEnd;
        public System.Action<int> OnBallInserted;
        /// <summary>
        /// Fired when two segments merge. Parameters:
        ///   mergedSegmentId — the ID of the surviving (ahead) segment that absorbed the other.
        ///   boundaryLocalIndex — the local index in the merged segment where the absorbed
        ///   segment's lead ball landed. Useful for detecting cascade matches at the seam.
        /// </summary>
        public System.Action<int, int> OnSegmentsMerged;
        /// <summary>
        /// Fired whenever a Hammer power-up ball is removed from the chain — by any means
        /// (projectile, Pierce, Bomb, or a match). Parameters:
        ///   globalChainIndex — the index the hammer occupied (captured pre-removal).
        ///   recoilDistance — the hammer's recoil distance (PowerUpValue).
        /// MatchProcessor listens to this to run the hammer recoil + cascade aftermath.
        /// </summary>
        public System.Action<int, float> OnHammerDestroyed;
        /// <summary>
        /// Fired whenever a *frozen* ball is removed from the chain — by any means
        /// (match, projectile, Pierce, Bomb, red explosion, or another icicle). Param is
        /// the ball's world position at removal time. IceSynergy listens and spawns an
        /// icicle from this position, enabling chain reactions through frozen balls.
        /// </summary>
        public System.Action<Vector3, int> OnFrozenBallDestroyed;
        /// <summary>
        /// Fired whenever a *primed* ball (Orange Conductor ignite) is removed from the chain
        /// by any means. Param is the ball's world position at removal time. ArcSynergy listens
        /// and detonates a mini-explosion there.
        /// </summary>
        public System.Action<Vector3, int> OnIgnitedBallDestroyed;

        private List<ChainSegment> segments = new List<ChainSegment>();
        private ObjectPool<Ball> ballPool;
        private bool isMoving = true;
        private int nextSegmentId = 0;

        // Per-frame timing of Update, read by the on-screen PerfOverlay so we can measure the
        // real cost in a WebGL build (the editor profiler only reaches Play mode, not the build).
        public static double UpdateMilliseconds;
        private readonly System.Diagnostics.Stopwatch updateStopwatch = new System.Diagnostics.Stopwatch();

        public float BallSpeed => ballSpeed;
        public float BallSpacing => ballSpacing;
        /// <summary>
        /// Transient speed multiplier applied to both forward motion and gap-close motion.
        /// 1.0 = normal speed. Used by IceSynergy's Chain Slowdown — when a blue match fires,
        /// it sets this below 1.0 for a window, then restores to 1.0. Clamped at SetChainSpeedMultiplier.
        /// </summary>
        public float ChainSpeedMultiplier { get; private set; } = 1f;

        public void SetChainSpeedMultiplier(float multiplier)
        {
            // Upper bound is generous so the wave-emergence surge can move fast. Blue ice
            // slowdown only ever sets values below 1, so the high cap doesn't affect it.
            ChainSpeedMultiplier = Mathf.Clamp(multiplier, 0.01f, 50f);
        }
        public int BallCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < segments.Count; i++) total += segments[i].Count;
                return total;
            }
        }

        private void Awake()
        {
            // Initialize in Awake (not Start) so the pool is ready before
            // LevelManager.Start kicks off LoadMap → BallSpawner.StartLevel,
            // which calls SpawnBall on this manager. Start-order between
            // MonoBehaviours is not guaranteed.
            if (ballPool == null)
                InitializePool(initialPoolSize);
        }

        /// <summary>
        /// Assigns the active PathController at runtime (called by LevelManager
        /// after a map prefab is instantiated). The PathController lives on the
        /// map prefab alongside its SplineContainer; this method lets the persistent
        /// scene systems rebind to it on each map load.
        /// </summary>
        public void SetPathController(PathController pc)
        {
            pathController = pc;
            if (pathController != null)
                pathController.InitializePath();
        }

        private void Update()
        {
            updateStopwatch.Restart();
            if (isMoving)
            {
                MoveChain(Time.deltaTime);
                MergeTouchingSegments();
                DecaySmoothShifts(Time.deltaTime);
                UpdateBallVisibility();
                UpdateBallPositions();
            }
            updateStopwatch.Stop();
            UpdateMilliseconds = updateStopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Eases each ball's smoothShift (path-progress) and worldOffset (world-space) toward
        /// zero. These offsets are set when a ball is inserted (or pushed by an insertion);
        /// decaying them makes the chain slide smoothly into its new positions instead of
        /// snapping, and lets a freshly-inserted ball glide along its trajectory into place.
        /// </summary>
        private void DecaySmoothShifts(float deltaTime)
        {
            if (insertionDuration <= 0f) return;

            float pathLength = pathController != null ? pathController.GetPathLength() : 0f;
            if (pathLength <= 0f) return;

            // Path-progress decay (one ball-spacing per insertionDuration in progress units).
            float pathDecay = (ballSpacing / pathLength) / insertionDuration * deltaTime;
            // World-space decay (one ball-spacing per insertionDuration in world units).
            float worldDecay = ballSpacing / insertionDuration * deltaTime;

            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];
                for (int i = 0; i < segment.Count; i++)
                {
                    var node = segment.balls[i];
                    if (node.smoothShift != 0f)
                        node.smoothShift = Mathf.MoveTowards(node.smoothShift, 0f, pathDecay);
                    if (node.worldOffset != Vector3.zero)
                        node.worldOffset = Vector3.MoveTowards(node.worldOffset, Vector3.zero, worldDecay);
                }
            }
        }

        public void InitializePool(int size)
        {
            if (ballPrefab == null)
            {
                Debug.LogError("BallChainManager: Ball prefab not assigned!");
                return;
            }

            ballPool = new ObjectPool<Ball>(ballPrefab, size, transform);
        }

        // --------------------------------------------------------------
        // Movement
        // --------------------------------------------------------------

        /// <summary>
        /// Movement is lead-driven from the front of the chain.
        /// - Single segment: the lead (ball at index 0 of the only segment) moves forward
        ///   at ballSpeed and all other balls follow at fixed spacing behind it.
        /// - Multiple segments: only the front segment moves, and it moves *backward*,
        ///   starting at gapCloseSpeed and accelerating by gapCloseAcceleration up to
        ///   gapCloseMaxSpeed. Every other segment is stationary. The chain only resumes
        ///   forward motion once the front segment has absorbed everything behind it
        ///   (i.e., the chain is one segment again); the accumulated speed resets then.
        /// </summary>
        private void MoveChain(float deltaTime)
        {
            if (pathController == null || segments.Count == 0) return;

            float pathLength = pathController.GetPathLength();
            if (pathLength <= 0f) return;

            float spacingProgress = ballSpacing / pathLength;

            ChainSegment front = segments[0];
            if (front.IsEmpty) return;

            BallNode lead = front.balls[0];

            if (segments.Count == 1)
            {
                // Single segment: forward motion. Reset gap-close acceleration so the
                // next gap starts from the base speed instead of inheriting the prior ramp.
                currentGapCloseSpeed = gapCloseSpeed;

                float forwardStep = ballSpeed * ChainSpeedMultiplier * deltaTime / pathLength;
                lead.pathProgress += forwardStep;
            }
            else
            {
                // Multi-segment: only the front moves, and it moves backward at an
                // accelerating speed clamped to gapCloseMaxSpeed.
                currentGapCloseSpeed = Mathf.Min(
                    currentGapCloseSpeed + gapCloseAcceleration * deltaTime,
                    gapCloseMaxSpeed);
                float gapCloseStep = currentGapCloseSpeed * ChainSpeedMultiplier * deltaTime / pathLength;
                lead.pathProgress -= gapCloseStep;
            }

            // Propagate spacing through the front segment from lead to tail.
            for (int i = 1; i < front.Count; i++)
            {
                front.balls[i].pathProgress = front.balls[i - 1].pathProgress - spacingProgress;
            }

            // Lose condition: the lead reached the end. Only checked when the chain is
            // one segment (in multi-segment mode the lead is moving backward, never forward).
            if (segments.Count == 1 && lead.pathProgress >= 1f)
            {
                lead.pathProgress = 1f;
                isMoving = false;
                OnBallReachedEnd?.Invoke();
                Debug.LogWarning("Ball reached the end! Game Over!");
            }
        }

        /// <summary>
        /// After movement, merges any segment whose lead has caught up to the segment ahead.
        /// Each merge fires OnSegmentsMerged with the boundary index. The handler may
        /// modify the segments list (e.g., cascade match removes balls and re-splits), so
        /// we restart the scan after every merge to keep iteration safe.
        /// </summary>
        private void MergeTouchingSegments()
        {
            if (segments.Count < 2) return;

            float pathLength = pathController.GetPathLength();
            if (pathLength <= 0f) return;

            float spacingProgress = ballSpacing / pathLength;
            float mergeTolerance = spacingProgress * 1.05f; // small slop so floating-point ties merge

            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int s = 1; s < segments.Count; s++)
                {
                    ChainSegment ahead = segments[s - 1];
                    ChainSegment behind = segments[s];

                    if (!ahead.IsEmpty && !behind.IsEmpty
                        && (ahead.Tail.pathProgress - behind.Lead.pathProgress) <= mergeTolerance)
                    {
                        int boundaryLocal = ahead.balls.Count; // index where behind's lead lands
                        int aheadId = ahead.id;

                        // Merge: append behind's balls to ahead, drop behind segment
                        for (int i = 0; i < behind.balls.Count; i++)
                        {
                            behind.balls[i].segmentId = ahead.id;
                            ahead.balls.Add(behind.balls[i]);
                        }
                        behind.balls.Clear();
                        segments.RemoveAt(s);

                        // Snap appended balls into tight spacing now, but record the
                        // displacement in smoothShift so they visually slide into their
                        // new positions instead of teleporting on the next propagation.
                        for (int i = boundaryLocal; i < ahead.balls.Count; i++)
                        {
                            var node = ahead.balls[i];
                            float oldProgress = node.pathProgress;
                            float newProgress = ahead.balls[i - 1].pathProgress - spacingProgress;
                            node.pathProgress = newProgress;
                            node.smoothShift += oldProgress - newProgress;
                        }

                        UpdateChainIndices();

                        // Notify subscribers — handler may modify the segments list
                        // (e.g., cascade match removes balls), so we restart the scan.
                        OnSegmentsMerged?.Invoke(aheadId, boundaryLocal);

                        merged = true;
                        break;
                    }
                }
            }
        }

        private void UpdateBallPositions()
        {
            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];
                for (int i = 0; i < segment.Count; i++)
                {
                    var node = segment.balls[i];
                    if (node.ball == null || !node.ball.gameObject.activeSelf) continue;

                    // Visual position uses smoothShift (path-progress) and worldOffset
                    // (world-space) on top of the logical pathProgress so insertions ease
                    // into place. Logical PathProgress (used by projectile hits, match
                    // detection, etc.) stays unshifted.
                    float visualProgress = Mathf.Max(node.pathProgress + node.smoothShift, holeProgress);

                    node.ball.transform.position = pathController.GetPointOnPath(visualProgress) + node.worldOffset;
                    node.ball.PathProgress = node.pathProgress;

                    Vector3 direction = pathController.GetDirectionOnPath(visualProgress);
                    if (direction != Vector3.zero)
                    {
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        node.ball.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
                    }
                }
            }
        }

        // --------------------------------------------------------------
        // Spawning (tail spawn from hole)
        // --------------------------------------------------------------

        /// <summary>
        /// Spawns a new ball at the hole, appending it to the back-most segment
        /// (or creating a new segment if the chain is empty).
        /// </summary>
        public void SpawnBall(BallColor color)
        {
            Ball ball = ballPool.Get();
            ball.Initialize(color);
            ball.OnGetFromPool();

            ChainSegment backSegment = GetBackSegment();
            if (backSegment == null)
            {
                backSegment = new ChainSegment(nextSegmentId++);
                segments.Add(backSegment);
            }

            float spawnProgress = CalculateSpawnProgress(backSegment);

            BallNode newNode = new BallNode(ball, spawnProgress, 0);
            newNode.segmentId = backSegment.id;
            backSegment.balls.Add(newNode);

            UpdateChainIndices();
        }

        private float CalculateSpawnProgress(ChainSegment backSegment)
        {
            if (backSegment.IsEmpty) return spawnProgress;

            float pathLength = pathController.GetPathLength();
            float spacingProgress = ballSpacing / pathLength;
            return backSegment.Tail.pathProgress - spacingProgress;
        }

        // --------------------------------------------------------------
        // Insertion (projectile hits)
        // --------------------------------------------------------------

        public void InsertBallAtProgress(BallColor color, float insertProgress, Vector3? projectileWorldPos = null)
        {
            Ball ball = ballPool.Get();
            ball.Initialize(color);
            ball.OnGetFromPool();

            float pathLength = pathController.GetPathLength();
            float spacingProgress = ballSpacing / pathLength;

            // Find segment + local index of the nearest ball
            ChainSegment hitSegment;
            int localHitIndex = FindNearestBallInSegments(insertProgress, out hitSegment);

            if (hitSegment == null)
            {
                // No balls anywhere — create a new segment with just this ball
                ChainSegment seg = new ChainSegment(nextSegmentId++);
                BallNode node = new BallNode(ball, insertProgress, 0);
                node.segmentId = seg.id;
                seg.balls.Add(node);
                segments.Add(seg);
                SortSegmentsByProgress();
                UpdateChainIndices();

                int globalIdx = GlobalIndexOf(node);
                Debug.Log($"Inserted ball into new segment - Color: {color}");
                OnBallInserted?.Invoke(globalIdx);
                return;
            }

            float hitProgress = hitSegment.balls[localHitIndex].pathProgress;
            int insertLocal;
            float insertAt;

            if (insertProgress >= hitProgress)
            {
                insertLocal = localHitIndex; // ahead of the hit ball
                insertAt = hitProgress + spacingProgress;
            }
            else
            {
                insertLocal = localHitIndex + 1; // behind the hit ball
                insertAt = hitProgress - spacingProgress;
            }

            BallNode newNode = new BallNode(ball, insertAt, 0);
            newNode.segmentId = hitSegment.id;

            // Start the new ball visually at the projectile's actual world position (often
            // off the path) so it slides into its target as the worldOffset decays to zero.
            // Falls back to a path-progress offset (visually starting at the projectile's
            // path-progress) if no world position was supplied by the caller.
            if (projectileWorldPos.HasValue && pathController != null)
            {
                Vector3 targetPathPos = pathController.GetPointOnPath(insertAt);
                newNode.worldOffset = projectileWorldPos.Value - targetPathPos;
            }
            else
            {
                newNode.smoothShift = insertProgress - insertAt;
            }
            hitSegment.balls.Insert(insertLocal, newNode);

            // Re-space within this segment. The push functions record the push amounts
            // into smoothShift so the affected balls visually lag behind their logical
            // positions and ease into place.
            PushSegmentBallsForward(hitSegment, insertLocal - 1, spacingProgress);
            PushSegmentBallsBackward(hitSegment, insertLocal + 1, spacingProgress);

            UpdateChainIndices();

            int globalInsertedIdx = GlobalIndexOf(newNode);
            Debug.Log($"Inserted ball at segment {hitSegment.id} local {insertLocal} - Color: {color}");
            OnBallInserted?.Invoke(globalInsertedIdx);
        }

        /// <summary>
        /// Pushes balls forward (toward the lead) to maintain spacing after an insertion.
        /// The amount each ball is shifted is recorded in its smoothShift so the visual
        /// position lags behind the logical position and eases forward smoothly.
        /// </summary>
        private void PushSegmentBallsForward(ChainSegment seg, int startLocal, float spacingProgress)
        {
            if (startLocal < 0) return;

            for (int i = startLocal; i >= 0; i--)
            {
                float requiredProgress = seg.balls[i + 1].pathProgress + spacingProgress;
                if (seg.balls[i].pathProgress < requiredProgress)
                {
                    float pushAmount = requiredProgress - seg.balls[i].pathProgress;
                    seg.balls[i].pathProgress = requiredProgress;
                    seg.balls[i].smoothShift -= pushAmount; // visual stays behind, eases forward
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Pushes balls backward (toward the tail) to maintain spacing after an insertion.
        /// Mirrors PushSegmentBallsForward — records the push amount in smoothShift so the
        /// visible balls keep their old positions and ease backward.
        /// </summary>
        private void PushSegmentBallsBackward(ChainSegment seg, int startLocal, float spacingProgress)
        {
            if (startLocal >= seg.Count) return;

            for (int i = startLocal; i < seg.Count; i++)
            {
                float requiredProgress = seg.balls[i - 1].pathProgress - spacingProgress;
                if (seg.balls[i].pathProgress > requiredProgress)
                {
                    float pushAmount = seg.balls[i].pathProgress - requiredProgress;
                    seg.balls[i].pathProgress = requiredProgress;
                    seg.balls[i].smoothShift += pushAmount; // visual stays ahead, eases backward
                }
                else
                {
                    break;
                }
            }
        }

        // --------------------------------------------------------------
        // Removal (matches, projectile hits, power-ups)
        // --------------------------------------------------------------

        /// <summary>
        /// Removes the given balls from their containing segments. Each segment is
        /// scanned for split points after removal — non-contiguous removals split
        /// a segment into multiple smaller segments.
        /// </summary>
        public void RemoveBalls(List<BallNode> nodesToRemove)
        {
            if (nodesToRemove == null || nodesToRemove.Count == 0) return;

            // Capture hammer info before the balls are recycled. Only one hammer can
            // exist at a time, so the first match in the batch is enough.
            bool hammerRemoved = false;
            int hammerIndex = -1;
            float hammerRecoil = 0f;
            
            foreach (var node in nodesToRemove)
            {
                if (node.ball != null && node.ball.PowerUpType == BallPowerUpType.Hammer)
                {
                    hammerRemoved = true;
                    hammerIndex = GlobalIndexOf(node);
                    hammerRecoil = node.ball.PowerUpValue;
                    break;
                }
            }

            // Capture frozen-ball positions before pooling so IceSynergy can spawn
            // icicles from where each frozen ball was. Chain reactions ride this hook.
            List<Vector3> frozenPositions = null;
            List<int> frozenPowers = null;
            foreach (var node in nodesToRemove)
            {
                if (node.isFrozen && node.ball != null)
                {
                    if (frozenPositions == null)
                    {
                        frozenPositions = new List<Vector3>(nodesToRemove.Count);
                        frozenPowers = new List<int>(nodesToRemove.Count);
                    }
                    frozenPositions.Add(node.ball.transform.position);
                    frozenPowers.Add(Mathf.Max(1, node.frozenPower));
                }
            }

            // Capture primed-ball positions before pooling so ArcSynergy can detonate a
            // mini-explosion from where each primed ball was (Orange Conductor ignite).
            List<Vector3> primedPositions = null;
            List<int> primedPowers = null;
            foreach (var node in nodesToRemove)
            {
                if (node.primed && node.ball != null)
                {
                    if (primedPositions == null)
                    {
                        primedPositions = new List<Vector3>(nodesToRemove.Count);
                        primedPowers = new List<int>(nodesToRemove.Count);
                    }
                    primedPositions.Add(node.ball.transform.position);
                    primedPowers.Add(Mathf.Max(1, node.ignitePower));
                }
            }

            // Group removals by segment
            var bySegment = new Dictionary<int, List<BallNode>>();
            foreach (var node in nodesToRemove)
            {
                if (!bySegment.TryGetValue(node.segmentId, out var list))
                {
                    list = new List<BallNode>();
                    bySegment[node.segmentId] = list;
                }
                list.Add(node);
            }

            foreach (var kvp in bySegment)
            {
                ChainSegment seg = GetSegmentById(kvp.Key);
                if (seg == null) continue;

                foreach (var node in kvp.Value)
                {
                    if (node.ball != null)
                    {
                        ballPool.Return(node.ball);
                        node.ball.OnReturnToPool();
                    }
                    seg.balls.Remove(node);
                }
            }

            SplitSegmentsAtGaps();
            PruneEmptySegments();
            UpdateChainIndices();

            if (hammerRemoved)
                OnHammerDestroyed?.Invoke(hammerIndex, hammerRecoil);

            if (frozenPositions != null)
            {
                for (int i = 0; i < frozenPositions.Count; i++)
                    OnFrozenBallDestroyed?.Invoke(frozenPositions[i], frozenPowers[i]);
            }

            if (primedPositions != null)
            {
                for (int i = 0; i < primedPositions.Count; i++)
                    OnIgnitedBallDestroyed?.Invoke(primedPositions[i], primedPowers[i]);
            }
        }

        /// <summary>
        /// Removes the ball at the given global chain index.
        /// </summary>
        public void RemoveBallAtIndex(int globalChainIndex)
        {
            BallNode node = GetNodeAtGlobalIndex(globalChainIndex);
            if (node == null) return;

            ChainSegment seg = GetSegmentById(node.segmentId);
            if (seg == null) return;

            // Capture hammer info before the ball is recycled.
            bool hammerRemoved = node.ball != null && node.ball.PowerUpType == BallPowerUpType.Hammer;
            float hammerRecoil = hammerRemoved ? node.ball.PowerUpValue : 0f;

            // Capture frozen position before pool return so IceSynergy can spawn an icicle.
            bool wasFrozen = node.isFrozen && node.ball != null;
            Vector3 frozenPos = wasFrozen ? node.ball.transform.position : Vector3.zero;
            int frozenPower = wasFrozen ? Mathf.Max(1, node.frozenPower) : 0;

            // Capture primed position before pool return so ArcSynergy can detonate it.
            bool wasPrimed = node.primed && node.ball != null;
            Vector3 primedPos = wasPrimed ? node.ball.transform.position : Vector3.zero;
            int primedPower = wasPrimed ? Mathf.Max(1, node.ignitePower) : 0;

            if (node.ball != null)
            {
                ballPool.Return(node.ball);
                node.ball.OnReturnToPool();
            }
            seg.balls.Remove(node);

            SplitSegmentsAtGaps();
            PruneEmptySegments();
            UpdateChainIndices();

            if (hammerRemoved)
                OnHammerDestroyed?.Invoke(globalChainIndex, hammerRecoil);

            if (wasFrozen)
                OnFrozenBallDestroyed?.Invoke(frozenPos, frozenPower);

            if (wasPrimed)
                OnIgnitedBallDestroyed?.Invoke(primedPos, primedPower);
        }

        /// <summary>
        /// Removes every ball within `radius` of `center` (an AoE blast): dedups by chain index and
        /// removes high-index-first so indices stay valid. Returns the number removed — callers hand
        /// that to MatchProcessor.ProcessPierceAftermath for cascades. Shared by the Bomb power-up,
        /// red-match explosions, and the Conductor ignite mini-blast.
        /// </summary>
        public int RemoveBallsInRadius(Vector3 center, float radius)
        {
            if (radius <= 0f) return 0;

            Collider[] hits = Physics.OverlapSphere(center, radius);
            List<int> indices = new List<int>();
            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i].CompareTag("Ball")) continue;
                Ball ball = hits[i].GetComponent<Ball>();
                if (ball != null && !indices.Contains(ball.ChainIndex))
                    indices.Add(ball.ChainIndex);
            }
            if (indices.Count == 0) return 0;

            indices.Sort((a, b) => b.CompareTo(a));   // high → low so earlier indices stay valid
            for (int i = 0; i < indices.Count; i++)
                RemoveBallAtIndex(indices[i]);

            return indices.Count;
        }

        /// <summary>
        /// Scans every segment for internal gaps wider than ballSpacing and splits them.
        /// </summary>
        private void SplitSegmentsAtGaps()
        {
            float pathLength = pathController.GetPathLength();
            if (pathLength <= 0f) return;

            float spacingProgress = ballSpacing / pathLength;
            float splitThreshold = spacingProgress * 1.5f;

            int s = 0;
            while (s < segments.Count)
            {
                ChainSegment seg = segments[s];
                int splitAt = -1;

                for (int i = 0; i < seg.Count - 1; i++)
                {
                    float gap = seg.balls[i].pathProgress - seg.balls[i + 1].pathProgress;
                    if (gap > splitThreshold)
                    {
                        splitAt = i + 1;
                        break;
                    }
                }

                if (splitAt > 0)
                {
                    ChainSegment newSeg = new ChainSegment(nextSegmentId++);
                    for (int i = splitAt; i < seg.Count; i++)
                    {
                        seg.balls[i].segmentId = newSeg.id;
                        newSeg.balls.Add(seg.balls[i]);
                    }
                    seg.balls.RemoveRange(splitAt, seg.Count - splitAt);
                    segments.Insert(s + 1, newSeg);
                    // Re-scan the same index in case the new segment also has gaps
                }
                else
                {
                    s++;
                }
            }
        }

        private void PruneEmptySegments()
        {
            for (int s = segments.Count - 1; s >= 0; s--)
            {
                if (segments[s].IsEmpty) segments.RemoveAt(s);
            }
        }

        // --------------------------------------------------------------
        // Hammer power-up insertion
        // --------------------------------------------------------------

        public void SpawnHammerBall(int insertAfterGlobalIndex, float recoilDistance)
        {
            BallNode anchor = GetNodeAtGlobalIndex(insertAfterGlobalIndex);
            if (anchor == null) return;

            ChainSegment seg = GetSegmentById(anchor.segmentId);
            if (seg == null) return;

            int localAnchor = seg.balls.IndexOf(anchor);
            if (localAnchor < 0) return;

            Ball ball = ballPool.Get();
            ball.Initialize(BallColor.Red); // color overridden by SetAsPowerUp visuals
            ball.SetAsPowerUp(BallPowerUpType.Hammer, recoilDistance);
            ball.OnGetFromPool();

            float pathLength = pathController.GetPathLength();
            float spacingProgress = ballSpacing / pathLength;

            float insertAt = anchor.pathProgress;

            BallNode newNode = new BallNode(ball, insertAt, 0);
            newNode.segmentId = seg.id;

            // Visually spawn the hammer ball off to the side of the path so it slides
            // in perpendicular to the chain (like a projectile would). The worldOffset
            // decays to zero, so the ball drifts into its logical position smoothly.
            Vector3 pathDir = pathController.GetDirectionOnPath(insertAt);
            Vector3 perpendicular = new Vector3(-pathDir.y, pathDir.x, 0f).normalized;
            float sideOffset = ballSpacing * 1.7f;
            newNode.worldOffset = perpendicular * sideOffset;

            seg.balls.Insert(localAnchor, newNode);

            PushSegmentBallsBackward(seg, localAnchor + 1, spacingProgress);
            UpdateChainIndices();

            Debug.Log($"Hammer ball spawned in segment {seg.id} at local {localAnchor}.");
        }

        // --------------------------------------------------------------
        // Visibility / hole logic
        // --------------------------------------------------------------

        private void UpdateBallVisibility()
        {
            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];
                for (int i = 0; i < segment.Count; i++)
                {
                    var node = segment.balls[i];
                    if (node.ball == null) continue;

                    // Include smoothShift so balls whose visual position is above the hole
                    // stay enabled even when their underlying pathProgress is below — e.g.
                    // after a merge that snapped pathProgress down while leaving smoothShift
                    // to preserve the visual position.
                    bool shouldBeVisible = node.pathProgress + node.smoothShift >= holeProgress;
                    if (node.ball.gameObject.activeSelf != shouldBeVisible)
                        node.ball.gameObject.SetActive(shouldBeVisible);
                }
            }
        }

        public bool HasVisibleBalls()
        {
            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];
                for (int i = 0; i < segment.Count; i++)
                {
                    var node = segment.balls[i];
                    if (node.pathProgress + node.smoothShift >= holeProgress) return true;
                }
            }
            return false;
        }

        // --------------------------------------------------------------
        // Internal helpers
        // --------------------------------------------------------------

        private void SortSegmentsByProgress()
        {
            segments.Sort((a, b) =>
            {
                if (a.IsEmpty) return 1;
                if (b.IsEmpty) return -1;
                return b.Lead.pathProgress.CompareTo(a.Lead.pathProgress);
            });
        }

        private void UpdateChainIndices()
        {
            int globalIndex = 0;
            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];
                for (int i = 0; i < segment.Count; i++)
                {
                    segment.balls[i].chainIndex = globalIndex;
                    segment.balls[i].segmentId = segment.id;
                    if (segment.balls[i].ball != null)
                        segment.balls[i].ball.ChainIndex = globalIndex;
                    globalIndex++;
                }
            }
        }

        private int FindNearestBallInSegments(float progress, out ChainSegment foundSegment)
        {
            foundSegment = null;
            int bestLocal = -1;
            float bestDistance = float.MaxValue;

            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];
                for (int i = 0; i < segment.Count; i++)
                {
                    float distance = Mathf.Abs(segment.balls[i].pathProgress - progress);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestLocal = i;
                        foundSegment = segment;
                    }
                }
            }

            return bestLocal;
        }

        private ChainSegment GetBackSegment()
        {
            if (segments.Count == 0) return null;
            return segments[segments.Count - 1];
        }

        private ChainSegment GetSegmentById(int id)
        {
            for (int s = 0; s < segments.Count; s++)
                if (segments[s].id == id) return segments[s];
            return null;
        }

        private BallNode GetNodeAtGlobalIndex(int globalIndex)
        {
            int running = 0;
            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];
                if (globalIndex < running + segment.Count)
                    return segment.balls[globalIndex - running];
                running += segment.Count;
            }
            return null;
        }

        private int GlobalIndexOf(BallNode node)
        {
            int running = 0;
            for (int s = 0; s < segments.Count; s++)
            {
                int local = segments[s].balls.IndexOf(node);
                if (local >= 0) return running + local;
                running += segments[s].Count;
            }
            return -1;
        }

        // --------------------------------------------------------------
        // Public API
        // --------------------------------------------------------------

        /// <summary>
        /// Returns a flattened front-to-back view of all balls in the chain.
        /// Useful for legacy iteration; segment boundaries are not visible in this view.
        /// </summary>
        public List<BallNode> GetBallChain()
        {
            var flat = new List<BallNode>(BallCount);
            for (int s = 0; s < segments.Count; s++)
                flat.AddRange(segments[s].balls);
            return flat;
        }

        /// <summary>
        /// Non-allocating variant of GetBallChain: clears and re-fills `buffer` with the front-to-back
        /// ball list. Lets per-hop / per-frame callers (e.g. the arc) reuse one buffer instead of
        /// allocating a fresh List every call.
        /// </summary>
        public void GetBallChainNonAlloc(List<BallNode> buffer)
        {
            buffer.Clear();
            for (int s = 0; s < segments.Count; s++)
                buffer.AddRange(segments[s].balls);
        }

        /// <summary>
        /// Returns the live list of segments. Mutating the returned list directly is unsafe;
        /// segments may be added, removed, or split during gameplay.
        /// </summary>
        public List<ChainSegment> GetSegments()
        {
            return segments;
        }

        /// <summary>
        /// Returns the segment containing the ball at the given global chain index, or null.
        /// </summary>
        public ChainSegment GetSegmentForChainIndex(int globalIndex)
        {
            BallNode node = GetNodeAtGlobalIndex(globalIndex);
            if (node == null) return null;
            return GetSegmentById(node.segmentId);
        }

        public void SetSpeed(float speed) { ballSpeed = speed; }
        public void SetMoving(bool moving) { isMoving = moving; }

        public void UpdateBallPositionsPublic() { UpdateBallPositions(); }
        public void UpdateBallVisibilityPublic() { UpdateBallVisibility(); }

        public float GetPathLength()
        {
            return pathController != null ? pathController.GetPathLength() : 0f;
        }

        public void ClearChain()
        {
            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];
                for (int i = 0; i < segment.Count; i++)
                {
                    var node = segment.balls[i];
                    if (node.ball != null)
                    {
                        ballPool.Return(node.ball);
                        node.ball.OnReturnToPool();
                    }
                }
            }
            segments.Clear();
            ChainSpeedMultiplier = 1f;
        }

        private void OnDestroy()
        {
            ClearChain();
        }
    }
}
