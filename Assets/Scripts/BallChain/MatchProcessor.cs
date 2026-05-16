using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YuumisProwl;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Drives the match pipeline for the segmented ball chain.
    ///
    /// Flow:
    ///   1. A match is detected (insertion or bomb/pierce aftermath).
    ///   2. ProcessMatches removes the matched balls. If they were in the middle of a segment,
    ///      the segment splits — the chain now has a gap.
    ///   3. With a gap present, BallChainManager's lead-driven movement only animates the
    ///      front-most segment, pulling it backward. All other segments are stationary.
    ///   4. As the front segment touches each segment behind it, BallChainManager fires
    ///      OnSegmentsMerged and we run a cascade match check at the merge boundary.
    ///      Any match found is processed immediately, which may re-split the chain and
    ///      drive another round of merges.
    ///   5. Once the front segment is the only segment left, the chain resumes forward motion.
    ///
    /// There is no explicit "recoil" animation in this flow — the visible "snap back" is the
    /// natural backward motion of the front segment as it closes gaps.
    /// </summary>
    public class MatchProcessor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;

        [Header("Match Settings")]
        [SerializeField] private float destructionDelay = 0.1f;
        [SerializeField] private bool enableDestructionEffects = true;
        [SerializeField] private float baseRecoilDistance = 0.3f;
        [SerializeField] private float recoilScalePerMatch = 0.2f;
        [SerializeField] private float maxRecoilDistance = 2f;
        [SerializeField] private float recoilDuration = 0.15f;

        private MatchDetector matchDetector = new MatchDetector();

        // Concurrent match sequences keyed by their tracked segment ID. The OnSegmentsMerged
        // handler routes cascade matches to the correct sequence by looking up the merged
        // segment's ID. Multiple sequences can be active at once (e.g. player inserts a
        // projectile that creates a match while another sequence is mid-gap-closing).
        private class MatchSequenceState
        {
            public int frontSegId;
            public int matchCount;
            public int lastGapGlobalIndex;
        }
        private Dictionary<int, MatchSequenceState> sequencesById = new Dictionary<int, MatchSequenceState>();

        public bool IsProcessingMatches => sequencesById.Count > 0;

        public System.Action<int, BallColor> OnBallsDestroyed;
        public System.Action OnChainCleared;
        /// <summary>
        /// Fired once after a match sequence completes (initial match + all cascades).
        /// Parameters: cascadeCount (0 = no cascades), lastGapGlobalIndex (-1 if chain cleared).
        /// </summary>
        public System.Action<int, int> OnMatchSequenceComplete;
        /// <summary>
        /// Fired immediately before RemoveBalls so listeners can capture each destroyed
        /// ball's world position. Parameters: positions, match color, cascadeIndex
        /// (0 = initial match in the sequence, 1+ = cascade). Gated by enableDestructionEffects.
        /// </summary>
        public System.Action<List<Vector3>, BallColor, int> OnMatchVisual;

        private void Start()
        {
            if (ballChainManager == null)
            {
                Debug.LogError("MatchProcessor: BallChainManager not assigned!");
                enabled = false;
                return;
            }

            ballChainManager.OnBallInserted += OnBallInserted;
            ballChainManager.OnSegmentsMerged += OnSegmentsMergedHandler;
            ballChainManager.OnHammerDestroyed += HandleHammerDestroyed;
        }

        private void OnDestroy()
        {
            if (ballChainManager != null)
            {
                ballChainManager.OnBallInserted -= OnBallInserted;
                ballChainManager.OnSegmentsMerged -= OnSegmentsMergedHandler;
                ballChainManager.OnHammerDestroyed -= HandleHammerDestroyed;
            }
        }

        /// <summary>
        /// Runs the hammer recoil + cascade aftermath whenever a Hammer ball is destroyed,
        /// regardless of what destroyed it (projectile, Pierce, Bomb, or a match).
        /// </summary>
        private void HandleHammerDestroyed(int hammerChainIndex, float recoilDistance)
        {
            ProcessHammerAftermath(hammerChainIndex, recoilDistance);
        }

        // --------------------------------------------------------------
        // Insertion-driven match detection
        // --------------------------------------------------------------

        private void OnBallInserted(int globalInsertedIndex)
        {
            ChainSegment seg = ballChainManager.GetSegmentForChainIndex(globalInsertedIndex);
            if (seg == null) return;

            // Capture the inserted node by reference so we can find it later even if the
            // segment is renumbered or absorbed during the destructionDelay wait.
            BallNode insertedNode = null;
            for (int i = 0; i < seg.Count; i++)
            {
                if (seg.balls[i].chainIndex == globalInsertedIndex)
                {
                    insertedNode = seg.balls[i];
                    break;
                }
            }
            if (insertedNode == null) return;

            StartCoroutine(CheckMatchesAfterInsertion(insertedNode));
        }

        private IEnumerator CheckMatchesAfterInsertion(BallNode insertedNode)
        {
            yield return new WaitForSeconds(destructionDelay);

            // Look up by the node's current segmentId — this is updated during merges,
            // so it always points to the live segment containing the ball.
            ChainSegment seg = FindSegmentById(insertedNode.segmentId);
            if (seg == null) yield break;

            int localIndex = seg.balls.IndexOf(insertedNode);
            if (localIndex < 0) yield break; // ball was destroyed during the wait

            List<BallNode> matched = matchDetector.DetectMatchAtIndex(seg.balls, localIndex);
            if (matched.Count == 0) yield break;

            if (sequencesById.TryGetValue(seg.id, out var existingSeq))
            {
                // A sequence is already active on this segment (e.g. mid-gap-closing).
                // Don't start a nested ProcessMatches — just remove the matched balls and
                // attribute them to the existing sequence. The existing WaitForBackMost
                // will continue, and any cascades from this removal are detected at merge
                // boundaries via the OnSegmentsMerged handler.
                int gapGlobalBeforeRemoval = matched[0].chainIndex;
                BallColor matchedColor = matched[0].ball.BallColor;
                int destroyedCount = matched.Count;

                FireMatchVisual(matched, matchedColor, existingSeq.matchCount);

                ballChainManager.RemoveBalls(matched);
                existingSeq.matchCount++;
                existingSeq.lastGapGlobalIndex = gapGlobalBeforeRemoval;

                OnBallsDestroyed?.Invoke(destroyedCount, matchedColor);
                Debug.Log($"Mid-sequence match: destroyed {destroyedCount} {matchedColor} balls.");

                if (ballChainManager.BallCount == 0)
                {
                    int cascadeCount = existingSeq.matchCount - 1;
                    OnMatchSequenceComplete?.Invoke(cascadeCount, -1);
                    OnChainCleared?.Invoke();
                    sequencesById.Clear();
                }
            }
            else
            {
                yield return StartCoroutine(ProcessMatches(seg, matched));
            }
        }

        // --------------------------------------------------------------
        // Merge-driven cascade detection
        // --------------------------------------------------------------

        /// <summary>
        /// Fires synchronously when BallChainManager merges two segments. If the active
        /// sequence is tracking the merged segment, we run a cascade match check at the
        /// merge boundary right at the moment of contact. Removing balls here may cause
        /// the segment to split again, which the lead-driven movement handles naturally.
        /// </summary>
        private void OnSegmentsMergedHandler(int segId, int boundaryLocal)
        {
            if (!sequencesById.TryGetValue(segId, out var seq)) return;

            ChainSegment seg = FindSegmentById(segId);
            if (seg == null || seg.IsEmpty) return;

            var matched = matchDetector.DetectMatchAtIndex(seg.balls, boundaryLocal);
            if (matched.Count == 0 && boundaryLocal > 0)
                matched = matchDetector.DetectMatchAtIndex(seg.balls, boundaryLocal - 1);

            if (matched.Count == 0) return;

            int gapGlobal = matched[0].chainIndex;
            BallColor color = matched[0].ball.BallColor;
            int count = matched.Count;

            FireMatchVisual(matched, color, seq.matchCount);

            ballChainManager.RemoveBalls(matched);
            seq.matchCount++;
            seq.lastGapGlobalIndex = gapGlobal;

            OnBallsDestroyed?.Invoke(count, color);
            Debug.Log($"Cascade match at merge: destroyed {count} {color} balls.");

            if (ballChainManager.BallCount == 0)
            {
                int cascadeCount = seq.matchCount - 1;
                OnMatchSequenceComplete?.Invoke(cascadeCount, -1);
                OnChainCleared?.Invoke();
                sequencesById.Clear();
            }
        }

        // --------------------------------------------------------------
        // Match processing pipeline
        // --------------------------------------------------------------

        private IEnumerator ProcessMatches(ChainSegment seg, List<BallNode> initialMatch)
        {
            int currentSegId = seg.id;

            // Reuse an existing sequence for this segment if one is already active; otherwise create one.
            if (!sequencesById.TryGetValue(currentSegId, out var sequenceState))
            {
                sequenceState = new MatchSequenceState
                {
                    frontSegId = currentSegId,
                    matchCount = 0,
                    lastGapGlobalIndex = -1,
                };
                sequencesById[currentSegId] = sequenceState;
            }

            List<BallNode> matchedBalls = initialMatch;

            while (matchedBalls.Count > 0)
            {
                int gapGlobalBeforeRemoval = matchedBalls[0].chainIndex;
                BallColor matchedColor = matchedBalls[0].ball.BallColor;
                int destroyedCount = matchedBalls.Count;

                // Detect whether the match touches the lead or tail of the current segment.
                // If it doesn't (mid-segment), removal will split the chain.
                ChainSegment matchedSeg = FindSegmentById(currentSegId);
                bool atLead = false;
                bool atTail = false;
                if (matchedSeg != null)
                {
                    int firstLocal = matchedSeg.balls.IndexOf(matchedBalls[0]);
                    int lastLocal = matchedSeg.balls.IndexOf(matchedBalls[matchedBalls.Count - 1]);
                    atLead = firstLocal == 0;
                    atTail = lastLocal == matchedSeg.Count - 1;
                }

                FireMatchVisual(matchedBalls, matchedColor, sequenceState.matchCount);

                int segCountBefore = ballChainManager.GetSegments().Count;
                ballChainManager.RemoveBalls(matchedBalls);
                sequenceState.matchCount++;
                sequenceState.lastGapGlobalIndex = gapGlobalBeforeRemoval;

                OnBallsDestroyed?.Invoke(destroyedCount, matchedColor);
                Debug.Log($"Destroyed {destroyedCount} {matchedColor} balls (segment {currentSegId}).");

                if (ballChainManager.BallCount == 0)
                {
                    OnMatchSequenceComplete?.Invoke(sequenceState.matchCount - 1, -1);
                    OnChainCleared?.Invoke();
                    sequencesById.Clear();
                    yield break;
                }

                bool splitHappened = ballChainManager.GetSegments().Count > segCountBefore;
                matchedBalls = new List<BallNode>();

                if (splitHappened)
                {
                    // Wait for the front segment to absorb everything behind it.
                    // During this wait, OnSegmentsMergedHandler fires for each merge and
                    // detects cascade matches at the contact point automatically.
                    yield return StartCoroutine(WaitForBackMost(currentSegId));
                }
                else
                {
                    // No split. Cascade only possible if the match was at the segment's lead
                    // or tail (now exposing a new lead/tail with potentially same-color balls).
                    ChainSegment afterSeg = FindSegmentById(currentSegId);
                    if (afterSeg != null && !afterSeg.IsEmpty)
                    {
                        int checkLocal = -1;
                        if (atLead) checkLocal = 0;
                        else if (atTail) checkLocal = afterSeg.Count - 1;

                        if (checkLocal >= 0 && checkLocal < afterSeg.Count)
                            matchedBalls = matchDetector.DetectMatchAtIndex(afterSeg.balls, checkLocal);
                    }
                }
            }

            // Apply the recoil "snap back" once the front segment has merged with the tail
            // (i.e., the chain is one segment again). Skip if our sequence was orphaned by
            // a merge (entry already removed from the dictionary).
            if (sequencesById.ContainsKey(currentSegId))
            {
                ChainSegment merged = FindSegmentById(currentSegId);
                if (merged != null && !merged.IsEmpty && sequenceState.matchCount > 0)
                {
                    float recoil = CalculateRecoilDistance(sequenceState.matchCount);
                    yield return StartCoroutine(ApplyChainRecoil(recoil, merged));
                }

                int cascadeCountFinal = sequenceState.matchCount - 1;
                if (cascadeCountFinal > 0)
                    Debug.Log($"Match sequence complete — {cascadeCountFinal} cascade(s).");
                OnMatchSequenceComplete?.Invoke(cascadeCountFinal, sequenceState.lastGapGlobalIndex);
                sequencesById.Remove(currentSegId);
            }
        }

        private float CalculateRecoilDistance(int matchNumber)
        {
            float recoil = baseRecoilDistance + (matchNumber - 1) * recoilScalePerMatch;
            return Mathf.Min(recoil, maxRecoilDistance);
        }

        // --------------------------------------------------------------
        // Wait helpers
        // --------------------------------------------------------------

        /// <summary>
        /// Waits until the segment with the given ID has absorbed every segment behind it
        /// (i.e., it's now at the last index of BallChainManager.GetSegments()). This is
        /// how we let a matching segment "pull back" through multiple gaps via natural motion.
        /// </summary>
        private IEnumerator WaitForBackMost(int segId, float timeout = 10f)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                var segs = ballChainManager.GetSegments();
                int idx = -1;
                for (int i = 0; i < segs.Count; i++)
                {
                    if (segs[i].id == segId) { idx = i; break; }
                }
                if (idx < 0) yield break; // segment was destroyed
                if (idx == segs.Count - 1) yield break; // back-most reached
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // --------------------------------------------------------------
        // External entry points (power-ups)
        // --------------------------------------------------------------

        /// <summary>
        /// Pushes a single specified segment back by `distance` world units.
        /// Used by the Hammer power-up.
        /// </summary>
        public void TriggerRecoil(float distance, ChainSegment segment)
        {
            if (segment == null) return;
            StartCoroutine(ApplyChainRecoil(distance, segment));
        }

        /// <summary>
        /// Handles the aftermath of a Hammer power-up consumption:
        ///   1. Recoils the back segment so the gap widens.
        ///   2. Registers a match sequence on the front segment so that when the
        ///      front segment closes the gap and merges, cascade detection runs at
        ///      the merge boundary (just like a regular match).
        ///   3. After the merge completes, applies a final "snap back" recoil to the
        ///      merged segment for visual feedback — matching the normal match flow.
        ///
        /// Call this after the hammer ball has been removed from the chain.
        /// </summary>
        public void ProcessHammerAftermath(int hammerOriginalChainIndex, float recoilDistance)
        {
            StartCoroutine(HammerAftermathRoutine(hammerOriginalChainIndex, recoilDistance));
        }

        private IEnumerator HammerAftermathRoutine(int hammerOriginalChainIndex, float recoilDistance)
        {
            // After the hammer was removed, the chain may have split. The given index now
            // points to the ball that was behind the hammer. A batch removal (Bomb / match)
            // can shift indices a lot — clamp into the surviving chain's range.
            int ballCount = ballChainManager.BallCount;
            if (ballCount <= 0) yield break;
            int idx = Mathf.Clamp(hammerOriginalChainIndex, 0, ballCount - 1);

            ChainSegment backSeg = ballChainManager.GetSegmentForChainIndex(idx);
            if (backSeg == null) yield break;

            // Find the segment ahead of the back segment — this is the one that will
            // close the gap and absorb the back segment via merging.
            var allSegs = ballChainManager.GetSegments();
            int backIdx = allSegs.IndexOf(backSeg);
            ChainSegment frontSeg = (backIdx > 0) ? allSegs[backIdx - 1] : null;

            // If there's no front segment (hammer was at the very front), just recoil and exit.
            if (frontSeg == null)
            {
                yield return StartCoroutine(ApplyChainRecoil(recoilDistance, backSeg));
                yield break;
            }

            // Register a sequence on the front segment so OnSegmentsMergedHandler runs
            // cascade detection when the gap closes. Count the hammer destruction as
            // one "match" so the final recoil calculation has a non-zero base.
            int currentSegId = frontSeg.id;

            // If a sequence is already tracking this segment (e.g. the hammer was caught
            // in an ongoing match), don't clobber it — that sequence's gap-close already
            // handles cascade detection. Just recoil and exit.
            if (sequencesById.ContainsKey(currentSegId))
            {
                yield return StartCoroutine(ApplyChainRecoil(recoilDistance, backSeg));
                yield break;
            }

            var sequenceState = new MatchSequenceState
            {
                frontSegId = currentSegId,
                matchCount = 1,
                lastGapGlobalIndex = idx,
            };
            sequencesById[currentSegId] = sequenceState;

            // Apply the initial recoil that creates the gap to close.
            yield return StartCoroutine(ApplyChainRecoil(recoilDistance, backSeg));

            // Wait for the front segment to absorb everything behind it. During this
            // wait, OnSegmentsMergedHandler fires for each merge and runs cascade detection
            // automatically. If a cascade match is found, it increments matchCount.
            yield return StartCoroutine(WaitForBackMost(currentSegId));

            // Apply the final "snap back" recoil to the merged segment.
            if (sequencesById.ContainsKey(currentSegId))
            {
                ChainSegment merged = FindSegmentById(currentSegId);
                if (merged != null && !merged.IsEmpty)
                {
                    float finalRecoil = CalculateRecoilDistance(sequenceState.matchCount);
                    yield return StartCoroutine(ApplyChainRecoil(finalRecoil, merged));
                }

                int cascadeCountFinal = sequenceState.matchCount - 1;
                OnMatchSequenceComplete?.Invoke(cascadeCountFinal, sequenceState.lastGapGlobalIndex);
                sequencesById.Remove(currentSegId);
            }
        }

        /// <summary>
        /// Backwards-compatible recoil entry: pushes back the segment containing the given
        /// global chain index. If no index is supplied (-1), recoils every segment.
        /// </summary>
        public void TriggerRecoil(float distance, int globalChainIndex = -1)
        {
            if (globalChainIndex < 0)
            {
                var segs = ballChainManager.GetSegments();
                for (int i = 0; i < segs.Count; i++)
                    StartCoroutine(ApplyChainRecoil(distance, segs[i]));
                return;
            }

            ChainSegment seg = ballChainManager.GetSegmentForChainIndex(globalChainIndex);
            if (seg != null)
                StartCoroutine(ApplyChainRecoil(distance, seg));
        }

        /// <summary>
        /// Power-up entry point: after a Pierce/Bomb removes balls, scan every segment for
        /// matches and process them serially. Each match runs through the full match pipeline,
        /// including merge-driven cascades.
        /// </summary>
        public void ProcessPierceAftermath(int destroyedCount)
        {
            if (destroyedCount > 0)
                OnBallsDestroyed?.Invoke(destroyedCount, BallColor.Red);

            if (ballChainManager.BallCount == 0)
            {
                OnMatchSequenceComplete?.Invoke(0, -1);
                OnChainCleared?.Invoke();
                return;
            }

            StartCoroutine(BombAftermathCoroutine());
        }

        private IEnumerator BombAftermathCoroutine()
        {
            // Brief pause so the visual destruction lands before cascade matches start firing.
            yield return new WaitForSeconds(destructionDelay);

            // Scan segments for matches and process them one at a time. Each ProcessMatches
            // call may re-split or merge the chain, so we re-scan after every iteration.
            while (true)
            {
                var segs = ballChainManager.GetSegments();
                ChainSegment segWithMatch = null;
                List<BallNode> firstMatch = null;

                for (int i = 0; i < segs.Count; i++)
                {
                    if (segs[i].IsEmpty) continue;
                    var matches = matchDetector.DetectAllMatches(segs[i].balls);
                    if (matches.Count > 0)
                    {
                        segWithMatch = segs[i];
                        firstMatch = matches[0];
                        break;
                    }
                }

                if (segWithMatch == null) yield break;

                yield return StartCoroutine(ProcessMatches(segWithMatch, firstMatch));
            }
        }

        // --------------------------------------------------------------
        // Per-segment recoil (used by power-ups, not by the match flow)
        // --------------------------------------------------------------

        public IEnumerator ApplyChainRecoil(float distance, ChainSegment segment)
        {
            if (segment == null || segment.IsEmpty) yield break;
            if (distance <= 0f) yield break;

            float pathLength = ballChainManager.GetPathLength();
            if (pathLength <= 0f) yield break;

            // Cap recoil so the segment's tail doesn't cross into the segment behind it.
            var allSegments = ballChainManager.GetSegments();
            int segIndex = allSegments.IndexOf(segment);
            if (segIndex >= 0 && segIndex + 1 < allSegments.Count)
            {
                ChainSegment behind = allSegments[segIndex + 1];
                if (!behind.IsEmpty)
                {
                    float spacingProgress = ballChainManager.BallSpacing / pathLength;
                    float maxProgress = segment.Tail.pathProgress - behind.Lead.pathProgress - spacingProgress;
                    float maxDistance = Mathf.Max(0f, maxProgress * pathLength);
                    distance = Mathf.Min(distance, maxDistance);
                }
            }
            if (distance <= 0f) yield break;

            float recoilProgress = distance / pathLength;

            BallNode[] snapshot = segment.balls.ToArray();
            float[] original = new float[snapshot.Length];
            for (int i = 0; i < snapshot.Length; i++) original[i] = snapshot[i].pathProgress;

            float elapsed = 0f;
            while (elapsed < recoilDuration)
            {
                float t = elapsed / recoilDuration;
                float lerp = Mathf.SmoothStep(0f, 1f, t);

                for (int i = 0; i < snapshot.Length; i++)
                    snapshot[i].pathProgress = original[i] - recoilProgress * lerp;

                ballChainManager.UpdateBallVisibilityPublic();
                ballChainManager.UpdateBallPositionsPublic();

                elapsed += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i].pathProgress = original[i] - recoilProgress;

            ballChainManager.UpdateBallVisibilityPublic();
            ballChainManager.UpdateBallPositionsPublic();
        }

        // --------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------

        private void FireMatchVisual(List<BallNode> matched, BallColor color, int cascadeIndex)
        {
            if (!enableDestructionEffects || OnMatchVisual == null) return;

            var positions = new List<Vector3>(matched.Count);
            for (int i = 0; i < matched.Count; i++)
            {
                if (matched[i].ball != null)
                    positions.Add(matched[i].ball.transform.position);
            }
            if (positions.Count > 0)
                OnMatchVisual.Invoke(positions, color, cascadeIndex);
        }

        private ChainSegment FindSegmentById(int id)
        {
            var segs = ballChainManager.GetSegments();
            for (int i = 0; i < segs.Count; i++)
                if (segs[i].id == id) return segs[i];
            return null;
        }

        private int LocalIndexOfGlobal(ChainSegment seg, int globalIndex)
        {
            if (seg == null) return -1;
            for (int i = 0; i < seg.Count; i++)
                if (seg.balls[i].chainIndex == globalIndex) return i;
            return -1;
        }
    }
}
