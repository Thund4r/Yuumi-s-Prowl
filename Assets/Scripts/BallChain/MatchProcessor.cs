using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YuumisProwl;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Owns the match detection pipeline: checks for matches after insertion,
    /// removes matched balls, closes gaps, applies recoil, and checks for cascades.
    /// Fires OnBallsDestroyed and OnChainCleared events consumed by GameManager.
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
        private bool isProcessingMatches = false;

        public bool IsProcessingMatches => isProcessingMatches;

        public System.Action<int, BallColor> OnBallsDestroyed;
        public System.Action OnChainCleared;
        /// <summary>
        /// Fired once after an entire match sequence (initial match + cascades) finishes.
        /// Parameters: cascadeCount (0 = no cascades), lastGapIndex (chain index where the
        /// final match occurred, or -1 if the chain was cleared).
        /// </summary>
        public System.Action<int, int> OnMatchSequenceComplete;

        private void Start()
        {
            if (ballChainManager == null)
            {
                Debug.LogError("MatchProcessor: BallChainManager not assigned!");
                enabled = false;
                return;
            }

            ballChainManager.OnBallInserted += OnBallInserted;
        }

        private void OnDestroy()
        {
            if (ballChainManager != null)
            {
                ballChainManager.OnBallInserted -= OnBallInserted;
            }
        }

        private void OnBallInserted(int insertedIndex)
        {
            StartCoroutine(CheckMatchesAfterInsertion(insertedIndex));
        }

        private IEnumerator CheckMatchesAfterInsertion(int insertedIndex)
        {
            if (isProcessingMatches) yield break;

            isProcessingMatches = true;
            yield return new WaitForSeconds(destructionDelay);

            List<BallNode> matchedBalls = matchDetector.DetectMatchAtIndex(ballChainManager.GetBallChain(), insertedIndex);

            if (matchedBalls.Count > 0)
            {
                yield return StartCoroutine(ProcessMatches(matchedBalls));
            }

            isProcessingMatches = false;
        }

        private IEnumerator ProcessMatches(List<BallNode> matchedBalls)
        {
            int matchCount = 0;
            int lastGapIndex = -1;

            while (matchedBalls.Count > 0)
            {
                var chain = ballChainManager.GetBallChain();
                int gapIndex = matchDetector.GetGapIndexAfterRemoval(chain, matchedBalls);
                BallColor matchedColor = matchedBalls[0].ball.BallColor;
                int destroyedCount = matchedBalls.Count;

                ballChainManager.RemoveBalls(matchedBalls);
                matchCount++;
                lastGapIndex = gapIndex;

                OnBallsDestroyed?.Invoke(destroyedCount, matchedColor);
                Debug.Log($"Destroyed {destroyedCount} {matchedColor} balls!");

                if (ballChainManager.GetBallChain().Count == 0)
                {
                    OnMatchSequenceComplete?.Invoke(matchCount - 1, -1);
                    OnChainCleared?.Invoke();
                    Debug.Log("All balls cleared! Level Complete!");
                    yield break;
                }

                yield return StartCoroutine(CloseGap(gapIndex));

                float recoil = CalculateRecoilDistance(matchCount);
                yield return StartCoroutine(ApplyChainRecoil(recoil));

                chain = ballChainManager.GetBallChain();
                if (gapIndex >= 0 && gapIndex < chain.Count)
                {
                    matchedBalls = matchDetector.DetectCascadeMatch(chain, gapIndex);
                    if (matchedBalls.Count > 0)
                    {
                        Debug.Log($"Cascade match found! {matchedBalls.Count} more balls!");
                    }
                }
                else
                {
                    break;
                }
            }

            int cascadeCount = matchCount - 1;
            if (cascadeCount > 0)
            {
                Debug.Log($"Match sequence complete — {cascadeCount} cascade(s).");
            }
            OnMatchSequenceComplete?.Invoke(cascadeCount, lastGapIndex);
        }

        private float CalculateRecoilDistance(int matchNumber)
        {
            float recoil = baseRecoilDistance + (matchNumber - 1) * recoilScalePerMatch;
            return Mathf.Min(recoil, maxRecoilDistance);
        }

        private IEnumerator CloseGap(int gapIndex)
        {
            var chain = ballChainManager.GetBallChain();
            if (gapIndex <= 0 || gapIndex >= chain.Count) yield break;

            float pathLength = ballChainManager.GetPathLength();
            if (pathLength <= 0f) yield break;

            float spacingProgress = ballChainManager.BallSpacing / pathLength;
            float timeout = 1.0f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                chain = ballChainManager.GetBallChain();
                if (gapIndex <= 0 || gapIndex >= chain.Count) break;

                float desired = chain[gapIndex].pathProgress + spacingProgress;
                float current = chain[gapIndex - 1].pathProgress;

                if (Mathf.Abs(current - desired) <= 0.0005f) break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Starts a chain recoil coroutine from any external caller (e.g. a power-up).
        /// Use this instead of calling StartCoroutine(ApplyChainRecoil()) from outside this class.
        /// </summary>
        public void TriggerRecoil(float distance)
        {
            StartCoroutine(ApplyChainRecoil(distance));
        }

        /// <summary>
        /// Called by the Pierce power-up after its projectile has finished traveling.
        /// Balls hit by the projectile were already removed during flight; this routine
        /// waits for the chain to settle, then detects and processes any matches/cascades
        /// that formed from the gap closures.
        /// </summary>
        public void ProcessPierceAftermath(int destroyedCount)
        {
            StartCoroutine(PierceAftermathCoroutine(destroyedCount));
        }

        private IEnumerator PierceAftermathCoroutine(int destroyedCount)
        {
            if (isProcessingMatches) yield break;
            isProcessingMatches = true;

            // Report the pierce destruction itself (color is irrelevant for pierce).
            if (destroyedCount > 0)
                OnBallsDestroyed?.Invoke(destroyedCount, BallColor.Red);

            if (ballChainManager.GetBallChain().Count == 0)
            {
                OnMatchSequenceComplete?.Invoke(0, -1);
                OnChainCleared?.Invoke();
                isProcessingMatches = false;
                yield break;
            }

            // Wait for the chain's natural spacing logic to pull gaps closed.
            yield return new WaitForSeconds(destructionDelay + 0.2f);

            int cascadeCount = 0;
            int lastGapIndex = -1;

            var chain = ballChainManager.GetBallChain();
            var allMatches = matchDetector.DetectAllMatches(chain);

            while (allMatches.Count > 0)
            {
                var match = allMatches[0];
                int gapIndex = matchDetector.GetGapIndexAfterRemoval(chain, match);
                BallColor matchedColor = match[0].ball.BallColor;
                int matchSize = match.Count;

                ballChainManager.RemoveBalls(match);
                cascadeCount++;
                lastGapIndex = gapIndex;

                OnBallsDestroyed?.Invoke(matchSize, matchedColor);
                Debug.Log($"Pierce cascade: destroyed {matchSize} {matchedColor} balls.");

                if (ballChainManager.GetBallChain().Count == 0)
                {
                    OnMatchSequenceComplete?.Invoke(cascadeCount, -1);
                    OnChainCleared?.Invoke();
                    isProcessingMatches = false;
                    yield break;
                }

                yield return StartCoroutine(CloseGap(gapIndex));
                yield return StartCoroutine(ApplyChainRecoil(CalculateRecoilDistance(cascadeCount)));

                chain = ballChainManager.GetBallChain();
                allMatches = matchDetector.DetectAllMatches(chain);
            }

            OnMatchSequenceComplete?.Invoke(cascadeCount, lastGapIndex);
            isProcessingMatches = false;
        }

        /// <summary>
        /// Applies a recoil (push-back) to the entire chain by a given world-space distance.
        /// Can be called externally by power-ups or other game systems.
        /// </summary>
        public IEnumerator ApplyChainRecoil(float distance)
        {
            var chain = ballChainManager.GetBallChain();
            if (chain.Count == 0) yield break;
            if (distance <= 0f) yield break;

            float pathLength = ballChainManager.GetPathLength();
            if (pathLength <= 0f) yield break;

            float recoilProgress = distance / pathLength;

            BallNode[] snapshot = chain.ToArray();
            float[] original = new float[snapshot.Length];
            for (int i = 0; i < snapshot.Length; i++) original[i] = snapshot[i].pathProgress;

            float elapsed = 0f;
            while (elapsed < recoilDuration)
            {
                float t = elapsed / recoilDuration;
                float lerp = Mathf.SmoothStep(0f, 1f, t);

                for (int i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i].pathProgress = original[i] - recoilProgress * lerp;
                }

                ballChainManager.UpdateBallVisibilityPublic();
                ballChainManager.UpdateBallPositionsPublic();

                elapsed += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i].pathProgress = original[i] - recoilProgress;
            }

            ballChainManager.UpdateBallVisibilityPublic();
            ballChainManager.UpdateBallPositionsPublic();
        }
    }
}
