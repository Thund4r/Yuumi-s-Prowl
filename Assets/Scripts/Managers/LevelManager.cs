using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using YuumisProwl.BallChain;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Manages level flow and scene transitions.
    ///
    /// On win:  balls stop → brief pause → transition scene plays (next level loads in background)
    ///          → animation finishes → new level pops in.
    /// On lose: balls stop → brief pause → retry scene reloads directly (no transition animation).
    ///
    /// Each playable scene is self-contained. Set the transition scene name once on this component.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private BallSpawner ballSpawner;
        [SerializeField] private GameManager gameManager;

        [Header("Level Config")]
        [SerializeField] private LevelData levelData;

        [Header("Transition")]
        [Tooltip("Name of the transition scene that plays between levels. " +
                 "Leave empty to skip the transition and load the next level directly.")]
        [SerializeField] private string transitionSceneName = "Transition";
        [Tooltip("Seconds between the level ending and the transition scene (or next level) loading.")]
        [SerializeField] private float pauseBeforeTransition = 0.5f;

        /// <summary>
        /// True from the moment a level ends until the next scene loads.
        /// ProjectileSpawner reads this to block shooting.
        /// </summary>
        public bool IsTransitioning { get; private set; }

        public LevelData CurrentLevel => levelData;

        private void Awake()
        {
            if (levelData != null)
                ApplyLevelSettings(levelData);
        }

        private void Start()
        {
            if (levelData == null)
                Debug.LogWarning("LevelManager: No LevelData assigned!");

            if (gameManager == null)
            {
                Debug.LogError("LevelManager: GameManager not assigned!");
                return;
            }

            gameManager.OnGameWon  += HandleLevelWon;
            gameManager.OnGameLost += HandleLevelLost;
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameWon  -= HandleLevelWon;
                gameManager.OnGameLost -= HandleLevelLost;
            }
        }

        // ── Win ─────────────────────────────────────────────────────────────────

        private void HandleLevelWon()
        {
            StartCoroutine(WinRoutine());
        }

        private IEnumerator WinRoutine()
        {
            IsTransitioning = true;

            if (ballChainManager != null)
                ballChainManager.SetMoving(false);

            yield return new WaitForSeconds(pauseBeforeTransition);

            string nextLevel = levelData != null ? levelData.nextSceneName : "";

            if (string.IsNullOrEmpty(nextLevel))
            {
                // No next level configured — this is the final level.
                Debug.Log("LevelManager: No next level set. This appears to be the final level.");
                IsTransitioning = false;
                yield break;
            }

            if (!string.IsNullOrEmpty(transitionSceneName))
            {
                // Store the destination so TransitionController can read it,
                // then load the transition scene.
                LevelTransitionData.NextSceneName = nextLevel;
                SceneManager.LoadScene(transitionSceneName);
            }
            else
            {
                // No transition scene — jump straight to the next level.
                SceneManager.LoadScene(nextLevel);
            }
        }

        // ── Lose ────────────────────────────────────────────────────────────────

        private void HandleLevelLost()
        {
            StartCoroutine(LoseRoutine());
        }

        private IEnumerator LoseRoutine()
        {
            IsTransitioning = true;

            if (ballChainManager != null)
                ballChainManager.SetMoving(false);

            yield return new WaitForSeconds(pauseBeforeTransition);

            string retryScene = (levelData != null && !string.IsNullOrEmpty(levelData.retrySceneName))
                ? levelData.retrySceneName
                : SceneManager.GetActiveScene().name;

            SceneManager.LoadScene(retryScene);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private void ApplyLevelSettings(LevelData data)
        {
            if (ballChainManager != null)
            {
                ballChainManager.InitializePool(data.totalBalls);
                ballChainManager.SetSpeed(data.ballSpeed);
            }

            if (ballSpawner != null)
            {
                ballSpawner.SetColorCount(data.colorCount);
                ballSpawner.SetTotalBalls(data.totalBalls);
            }
        }
    }
}
