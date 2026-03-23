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

            while (matchedBalls.Count > 0)
            {
                var chain = ballChainManager.GetBallChain();
                int gapIndex = matchDetector.GetGapIndexAfterRemoval(chain, matchedBalls);
                BallColor matchedColor = matchedBalls[0].ball.BallColor;
                int destroyedCount = matchedBalls.Count;

                ballChainManager.RemoveBalls(matchedBalls);
                matchCount++;

                OnBallsDestroyed?.Invoke(destroyedCount, matchedColor);
                Debug.Log($"Destroyed {destroyedCount} {matchedColor} balls!");

                if (ballChainManager.GetBallChain().Count == 0)
                {
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
