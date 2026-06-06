using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Handles game state and win/lose conditions.
    /// Win conditions (all converge on WinGame, which guards against double-firing):
    ///   1. MatchProcessor.OnChainCleared event (fast path when matches/bombs empty the chain).
    ///   2. Per-frame: gameplay has started AND no balls are visible on screen. Catches
    ///      every other case — chain emptied via icicles, chain cleared down to the
    ///      invisible queue, chain pushed entirely below the hole by gap-close, etc.
    /// Lose condition: lead ball reaches the end of the path.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private BallSpawner ballSpawner;
        [SerializeField] private BossManager bossManager;

        // Game state
        private bool gameOver = false;
        private bool levelComplete = false;
        private bool gameplayStarted = false;
        public bool waveCleared = false;

        // Events
        public System.Action OnGameWon;
        public System.Action OnGameLost;
        public System.Action OnWaveCleared;

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
            if (bossManager != null)
                bossManager.OnBossDefeated += WinGame;
        }

        private void Update()
        {
            if (gameOver || levelComplete) return;

            bool introPlaying = ballSpawner != null && ballSpawner.IsPlayingIntro;
            if (introPlaying) return;

            if (!gameplayStarted) return;

            // Single converged win condition: no balls on screen. Covers chain-emptied
            // (BallCount == 0 → HasVisibleBalls trivially false), chain-cleared-to-queue
            // (queue balls exist below hole, none visible), and chain-retreated (gap-close
            // pushed everything below hole). The earlier OnChainCleared event handler is
            // a faster-firing duplicate of the empty-chain case.
            if (!ballChainManager.HasVisibleBalls())
            {
                if (!waveCleared)
                {
                    waveCleared = true;
                    HandleWaveCleared();
                }
            }
            else
            {
                waveCleared = false;
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
            if (bossManager != null)
                bossManager.OnBossDefeated -= WinGame;
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
        

        private void HandleWaveCleared()
        {
            // Wave damage now flies to the boss as a bolt, so "did the boss survive?" is no longer
            // synchronous — BossManager calls back once the chunk lands and the boss is still alive.
            bossManager.HandleWaveCleared(HandleBossSurvivedWave);
        }

        private void HandleBossSurvivedWave()
        {
            ballSpawner.SpawnNextWave();
            OnWaveCleared?.Invoke();
        }

        /// <summary>
        /// Handles chain cleared event. All balls were destroyed via matches —
        /// always a win regardless of how many were left to spawn, because
        /// tail spawning stops when the chain is empty.
        /// </summary>
        private void HandleChainCleared()
        {
            if (!waveCleared && !gameOver)
            {
                waveCleared = true;
                HandleWaveCleared();
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

    }
}
