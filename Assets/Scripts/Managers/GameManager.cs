using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Handles game state and win/lose conditions.
    /// Win conditions:
    ///   1. Chain cleared and no more balls to spawn (all matched).
    ///   2. No visible balls on screen (line retreated fully into hole).
    /// Lose condition:
    ///   Lead ball reaches the end of the path.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private BallSpawner ballSpawner;

        // Game state
        private bool gameOver = false;
        private bool levelComplete = false;

        // Events
        public System.Action OnGameWon;
        public System.Action OnGameLost;

        public bool IsGameOver => gameOver;
        public bool IsLevelComplete => levelComplete;

        private void Start()
        {
            if (ballChainManager == null)
            {
                Debug.LogError("GameManager: BallChainManager not assigned!");
                return;
            }

            if (matchProcessor == null)
            {
                Debug.LogError("GameManager: MatchProcessor not assigned!");
                return;
            }

            matchProcessor.OnChainCleared += HandleChainCleared;
            ballChainManager.OnBallReachedEnd += HandleBallReachedEnd;

            InitializeGame();
        }

        private void Update()
        {
            if (gameOver || levelComplete) return;

            // Win if every ball in the chain has retreated behind the hole (none visible).
            // AllBallsSpawned is intentionally NOT checked here: recoil stops tail spawning
            // before the total is reached, so that flag would never become true in this scenario.
            bool introPlaying = ballSpawner != null && ballSpawner.IsPlayingIntro;
            if (!introPlaying
                && ballChainManager.BallCount > 0
                && !ballChainManager.HasVisibleBalls())
            {
                WinGame();
            }
        }

        private void OnDestroy()
        {
            if (matchProcessor != null)
                matchProcessor.OnChainCleared -= HandleChainCleared;
            if (ballChainManager != null)
                ballChainManager.OnBallReachedEnd -= HandleBallReachedEnd;
        }

        /// <summary>
        /// Resets all in-game state. Called by LevelManager on level load.
        /// </summary>
        public void InitializeGame()
        {
            gameOver = false;
            levelComplete = false;
            Debug.Log("Game initialised.");
        }

        /// <summary>
        /// Handles chain cleared event. Only wins if no more balls are queued —
        /// otherwise the tail spawn will keep filling the chain.
        /// </summary>
        private void HandleChainCleared()
        {
            if (gameOver) return;

            if (ballSpawner == null || ballSpawner.AllBallsSpawned)
            {
                Debug.Log("Level Complete — all balls cleared!");
                WinGame();
            }
        }

        /// <summary>
        /// Handles ball reaching the end (lose condition).
        /// </summary>
        private void HandleBallReachedEnd()
        {
            if (gameOver || levelComplete) return;

            Debug.LogWarning("Game Over — ball reached the end!");
            LoseGame();
        }

        private void WinGame()
        {
            if (gameOver || levelComplete) return;

            levelComplete = true;
            gameOver = true;

            OnGameWon?.Invoke();
            Debug.Log("=== VICTORY ===");
        }

        private void LoseGame()
        {
            if (gameOver || levelComplete) return;

            gameOver = true;

            OnGameLost?.Invoke();
            Debug.Log("=== GAME OVER ===");
        }

        /// <summary>
        /// Full restart: clears the chain, resets the spawner, and re-initialises state.
        /// </summary>
        public void RestartGame()
        {
            if (ballChainManager != null)
                ballChainManager.ClearChain();
            if (ballSpawner != null)
                ballSpawner.StartLevel();
            InitializeGame();
        }
    }
}
