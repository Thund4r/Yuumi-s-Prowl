using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Loads levels and applies their settings to the relevant systems.
    /// Settings are applied in Awake so BallSpawner.Start() picks them up correctly.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private BallSpawner ballSpawner;
        [SerializeField] private GameManager gameManager;

        [Header("Levels")]
        [SerializeField] private LevelData[] levels;

        private int currentLevelIndex = 0;

        public LevelData CurrentLevel => (levels != null && currentLevelIndex < levels.Length) ? levels[currentLevelIndex] : null;
        public int CurrentLevelIndex => currentLevelIndex;
        public int LevelCount => levels != null ? levels.Length : 0;

        private void Awake()
        {
            if (levels != null && levels.Length > 0)
                ApplyLevelSettings(levels[0]);
        }

        private void Start()
        {
            if (levels == null || levels.Length == 0)
                Debug.LogWarning("LevelManager: No levels configured!");
        }

        /// <summary>
        /// Loads a level by index. Clears the current chain and restarts the spawner.
        /// Safe to call during gameplay for level transitions.
        /// </summary>
        public void LoadLevel(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length)
            {
                Debug.LogError($"LevelManager: Level index {index} is out of range.");
                return;
            }

            currentLevelIndex = index;
            LevelData data = levels[index];

            ApplyLevelSettings(data);

            if (ballChainManager != null)
                ballChainManager.ClearChain();

            if (ballSpawner != null)
                ballSpawner.StartLevel();

            if (gameManager != null)
                gameManager.InitializeGame();

            Debug.Log($"Loaded level {index + 1}: {data.levelName}");
        }

        /// <summary>
        /// Advances to the next level. Logs a warning if already on the last level.
        /// </summary>
        public void LoadNextLevel()
        {
            if (currentLevelIndex + 1 < LevelCount)
                LoadLevel(currentLevelIndex + 1);
            else
                Debug.Log("LevelManager: No more levels.");
        }

        private void ApplyLevelSettings(LevelData data)
        {
            if (ballChainManager != null)
                ballChainManager.SetSpeed(data.ballSpeed);

            if (ballSpawner != null)
            {
                ballSpawner.SetColorCount(data.colorCount);
                ballSpawner.SetTotalBalls(data.totalBalls);
            }
        }
    }
}
