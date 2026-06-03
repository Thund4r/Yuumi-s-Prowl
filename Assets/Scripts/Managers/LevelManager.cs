using UnityEngine;
using System.Collections;
using YuumisProwl.BallChain;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Loads map prefabs on demand. After the progression refactor, LevelManager is a
    /// pure loader: a caller (typically RunManager) passes a Map prefab to LoadMap()
    /// and LevelManager handles the teardown → pause → instantiate → bind → start cycle.
    ///
    /// Win/lose handling and "which map is next" live in RunManager, not here.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private BallSpawner ballSpawner;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BossManager bossManager;

        [Header("Maps")]
        [Tooltip("Where instantiated maps are parented. Leave null to parent to this transform.")]
        [SerializeField] private Transform mapRoot;

        [Header("Transition")]
        [Tooltip("Seconds between teardown of the old map and instantiation of the new one.")]
        [SerializeField] private float pauseBetweenMaps = 0.5f;

        public bool IsTransitioning { get; private set; }
        public Map CurrentMap => currentMapInstance;
        public LevelData CurrentLevel => currentMapInstance != null ? currentMapInstance.LevelData : null;

        private Map currentMapInstance;
        private Coroutine loadRoutine;

        private void Awake()
        {
            if (mapRoot == null) mapRoot = transform;
        }

        /// <summary>
        /// Loads the given map prefab: tears down the current map, pauses briefly,
        /// instantiates the new one, binds its PathController and LevelData into the
        /// persistent systems, and starts the level.
        ///
        /// Optional multipliers scale the map's baseline LevelData values for this load.
        /// `colorCountCap` (> 0) clamps `LevelData.colorCount` to no more than the cap —
        /// used by RunManager to enforce the player's currently-unlocked colour count.
        /// Pass -1 (default) to use the LevelData value unchanged.
        /// </summary>
        public void LoadMap(Map prefab, float ballSpeedMult = 1f, int colorCountCap = -1, float bossHealthMult = 1f)
        {
            if (prefab == null)
            {
                Debug.LogError("LevelManager.LoadMap: prefab is null.");
                return;
            }

            if (loadRoutine != null) StopCoroutine(loadRoutine);
            loadRoutine = StartCoroutine(LoadMapRoutine(prefab, ballSpeedMult, colorCountCap, bossHealthMult));
        }

        private IEnumerator LoadMapRoutine(Map prefab, float ballSpeedMult, int colorCountCap, float bossHealthMult)
        {
            IsTransitioning = true;

            if (ballChainManager != null)
                ballChainManager.SetMoving(false);

            yield return new WaitForSeconds(pauseBetweenMaps);

            if (currentMapInstance != null)
            {
                Destroy(currentMapInstance.gameObject);
                currentMapInstance = null;
            }

            if (ballChainManager != null)
                ballChainManager.ClearChain();

            currentMapInstance = Instantiate(prefab, mapRoot);

            if (ballChainManager != null)
                ballChainManager.SetPathController(currentMapInstance.PathController);

            LevelData data = currentMapInstance.LevelData;
            if (data != null)
            {
                float effectiveSpeed = data.ballSpeed * ballSpeedMult;

                if (ballChainManager != null)
                    ballChainManager.SetSpeed(effectiveSpeed);

                if (ballSpawner != null)
                {
                    int effectiveColorCount = data.colorCount;
                    if (colorCountCap > 0)
                        effectiveColorCount = Mathf.Min(effectiveColorCount, colorCountCap);
                    ballSpawner.SetColorCount(effectiveColorCount);
                }
            }

            if (bossManager != null)
                bossManager.SpawnBoss(currentMapInstance.BossSpawnPoint, bossHealthMult);

            if (gameManager != null)
                gameManager.InitializeGame();

            if (ballChainManager != null)
                ballChainManager.SetMoving(true);

            if (ballSpawner != null)
                ballSpawner.StartLevel();

            IsTransitioning = false;
            loadRoutine = null;
        }
    }
}
