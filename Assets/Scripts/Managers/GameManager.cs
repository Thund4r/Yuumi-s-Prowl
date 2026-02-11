using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Main game manager handling game state, scoring, and win/lose conditions.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;

        [Header("Game Settings")]
        [SerializeField] private int targetScore = 1000;
        [SerializeField] private int basePointsPerBall = 10;
        [SerializeField] private float comboMultiplier = 1.5f;

        // Game state
        private int currentScore = 0;
        private int currentCombo = 0;
        private bool gameOver = false;
        private bool levelComplete = false;

        // Events
        public System.Action<int> OnScoreChanged;
        public System.Action<int> OnComboChanged;
        public System.Action OnGameWon;
        public System.Action OnGameLost;

        public int CurrentScore => currentScore;
        public int CurrentCombo => currentCombo;
        public bool IsGameOver => gameOver;
        public bool IsLevelComplete => levelComplete;

        private void Start()
        {
            if (ballChainManager == null)
            {
                Debug.LogError("GameManager: BallChainManager not assigned!");
                return;
            }

            // Subscribe to ball chain events
            ballChainManager.OnBallsDestroyed += HandleBallsDestroyed;
            ballChainManager.OnChainCleared += HandleChainCleared;
            ballChainManager.OnBallReachedEnd += HandleBallReachedEnd;

            InitializeGame();
        }

        private void OnDestroy()
        {
            if (ballChainManager != null)
            {
                ballChainManager.OnBallsDestroyed -= HandleBallsDestroyed;
                ballChainManager.OnChainCleared -= HandleChainCleared;
                ballChainManager.OnBallReachedEnd -= HandleBallReachedEnd;
            }
        }

        private void InitializeGame()
        {
            currentScore = 0;
            currentCombo = 0;
            gameOver = false;
            levelComplete = false;

            OnScoreChanged?.Invoke(currentScore);
            OnComboChanged?.Invoke(currentCombo);

            Debug.Log($"Game Started! Target Score: {targetScore}");
        }

        /// <summary>
        /// Handles ball destruction events and updates score.
        /// </summary>
        private void HandleBallsDestroyed(int count, BallColor color)
        {
            if (gameOver || levelComplete) return;

            // Increment combo
            currentCombo++;

            // Calculate points with combo multiplier
            int basePoints = count * basePointsPerBall;
            float multiplier = 1f + (currentCombo - 1) * (comboMultiplier - 1f);
            int points = Mathf.RoundToInt(basePoints * multiplier);

            // Add to score
            currentScore += points;

            OnScoreChanged?.Invoke(currentScore);
            OnComboChanged?.Invoke(currentCombo);

            Debug.Log($"Score: +{points} (Combo x{currentCombo}) | Total: {currentScore}/{targetScore}");

            // Check for win condition
            if (currentScore >= targetScore)
            {
                WinGame();
            }
        }

        /// <summary>
        /// Handles chain cleared event (all balls destroyed).
        /// </summary>
        private void HandleChainCleared()
        {
            if (gameOver) return;

            Debug.Log("Level Complete - All balls cleared!");
            WinGame();
        }

        /// <summary>
        /// Handles ball reaching the end (lose condition).
        /// </summary>
        private void HandleBallReachedEnd()
        {
            if (gameOver || levelComplete) return;

            Debug.LogWarning("Game Over - Ball reached the end!");
            LoseGame();
        }

        /// <summary>
        /// Triggers win condition.
        /// </summary>
        private void WinGame()
        {
            if (gameOver || levelComplete) return;

            levelComplete = true;
            gameOver = true;

            OnGameWon?.Invoke();

            Debug.Log($"=== VICTORY ===");
            Debug.Log($"Final Score: {currentScore}");
            Debug.Log($"Max Combo: {currentCombo}");
        }

        /// <summary>
        /// Triggers lose condition.
        /// </summary>
        private void LoseGame()
        {
            if (gameOver || levelComplete) return;

            gameOver = true;

            OnGameLost?.Invoke();

            Debug.Log($"=== GAME OVER ===");
            Debug.Log($"Final Score: {currentScore}/{targetScore}");
        }

        /// <summary>
        /// Resets combo counter (called when no matches occur).
        /// </summary>
        public void ResetCombo()
        {
            if (currentCombo > 0)
            {
                Debug.Log($"Combo broken! Was at x{currentCombo}");
                currentCombo = 0;
                OnComboChanged?.Invoke(currentCombo);
            }
        }

        /// <summary>
        /// Restarts the game.
        /// </summary>
        public void RestartGame()
        {
            ballChainManager.ClearChain();
            InitializeGame();
        }

        /// <summary>
        /// Sets the target score for winning.
        /// </summary>
        public void SetTargetScore(int score)
        {
            targetScore = score;
        }
    }
}
