using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Handles game state and win/lose conditions.
    /// Win conditions:
    ///   1. MatchProcessor.OnChainCleared event fires (fast path for match/bomb/pierce).
    ///   2. Per-frame robust check: chain is empty after gameplay has started — catches
    ///      icicles and any other path that removes balls without firing OnChainCleared.
    ///   3. Per-frame retreat check: balls exist but none visible (chain pulled back into hole).
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
        private bool gameplayStarted = false;

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
            if (ballSpawner != null)
                ballSpawner.OnIntroComplete += HandleIntroComplete;
        }

        private void Update()
        {
            if (gameOver || levelComplete) return;

            bool introPlaying = ballSpawner != null && ballSpawner.IsPlayingIntro;
            if (introPlaying) return;

            // Robust empty-chain win: any path that empties the chain after gameplay has
            // started counts as a win, even if OnChainCleared wasn't fired (e.g. icicles,
            // future synergies, anything that calls RemoveBallAtIndex directly).
            if (gameplayStarted && ballChainManager.BallCount == 0)
            {
                WinGame();
                return;
            }

            // Retreat win: balls exist but none are visible (chain fully pulled back into the hole).
            if (ballChainManager.BallCount > 0 && !ballChainManager.HasVisibleBalls())
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
            if (ballSpawner != null)
                ballSpawner.OnIntroComplete -= HandleIntroComplete;
        }

        /// <summary>
        /// Resets all in-game state. Called by LevelManager on level load.
        /// </summary>
        public void InitializeGame()
        {
            gameOver = false;
            levelComplete = false;
            gameplayStarted = false;
            Debug.Log("Game initialised.");
        }

        private void HandleIntroComplete()
        {
            gameplayStarted = true;
        }

        /// <summary>
        /// Handles chain cleared event. All balls were destroyed via matches —
        /// always a win regardless of how many were left to spawn, because
        /// tail spawning stops when the chain is empty.
        /// </summary>
        private void HandleChainCleared()
        {
            if (gameOver) return;

            Debug.Log("Level Complete — all balls cleared!");
            WinGame();
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

    }
}
