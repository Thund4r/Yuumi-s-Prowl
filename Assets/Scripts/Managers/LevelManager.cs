using UnityEngine;
using System.Collections;
using YuumisProwl.BallChain;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Loads map prefabs into the active scene. Each map prefab carries its own
    /// PathController (with SplineContainer), obstacles, decorations, and LevelData.
    /// LevelManager instantiates a map, wires its PathController into the persistent
    /// BallChainManager, applies the map's LevelData, and triggers BallSpawner.
    ///
    /// On win:  advance to the next map (or loop / stop on final).
    /// On lose: re-instantiate the current map.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private BallSpawner ballSpawner;
        [SerializeField] private GameManager gameManager;

        [Header("Maps")]
        [Tooltip("Map prefabs played in order. Each prefab's root must have a Map component.")]
        [SerializeField] private Map[] mapPrefabs;
        [Tooltip("Where instantiated maps are parented. Leave null to parent to this transform.")]
        [SerializeField] private Transform mapRoot;
        [Tooltip("If true, after the final map is won the loader wraps back to map 0.")]
        [SerializeField] private bool loopMaps = true;

        [Header("Transition")]
        [Tooltip("Seconds between a map ending and the next/retry map loading.")]
        [SerializeField] private float pauseBetweenMaps = 0.5f;

        public bool IsTransitioning { get; private set; }
        public Map CurrentMap => currentMapInstance;
        public LevelData CurrentLevel => currentMapInstance != null ? currentMapInstance.LevelData : null;

        private Map currentMapInstance;
        private int currentMapIndex = 0;

        private void Awake()
        {
            if (mapRoot == null) mapRoot = transform;
        }

        private void Start()
        {
            if (gameManager == null)
            {
                Debug.LogError("LevelManager: GameManager not assigned!");
                return;
            }

            gameManager.OnGameWon  += HandleLevelWon;
            gameManager.OnGameLost += HandleLevelLost;

            if (mapPrefabs == null || mapPrefabs.Length == 0)
            {
                Debug.LogError("LevelManager: No map prefabs assigned!");
                return;
            }

            LoadMap(0);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameWon  -= HandleLevelWon;
                gameManager.OnGameLost -= HandleLevelLost;
            }
        }

        private void LoadMap(int index)
        {
            if (mapPrefabs == null || mapPrefabs.Length == 0) return;
            index = Mathf.Clamp(index, 0, mapPrefabs.Length - 1);

            if (currentMapInstance != null)
            {
                Destroy(currentMapInstance.gameObject);
                currentMapInstance = null;
            }

            if (ballChainManager != null)
                ballChainManager.ClearChain();

            currentMapIndex = index;
            currentMapInstance = Instantiate(mapPrefabs[index], mapRoot);

            if (ballChainManager != null)
                ballChainManager.SetPathController(currentMapInstance.PathController);

            LevelData data = currentMapInstance.LevelData;
            if (data != null)
            {
                if (ballChainManager != null)
                    ballChainManager.SetSpeed(data.ballSpeed);

                if (ballSpawner != null)
                {
                    ballSpawner.SetColorCount(data.colorCount);
                    ballSpawner.SetTotalBalls(data.totalBalls);
                }
            }

            if (gameManager != null)
                gameManager.InitializeGame();

            if (ballChainManager != null)
                ballChainManager.SetMoving(true);

            if (ballSpawner != null)
                ballSpawner.StartLevel();

            IsTransitioning = false;
        }

        private void HandleLevelWon()
        {
            StartCoroutine(AdvanceAfterPause());
        }

        private IEnumerator AdvanceAfterPause()
        {
            IsTransitioning = true;

            if (ballChainManager != null)
                ballChainManager.SetMoving(false);

            yield return new WaitForSeconds(pauseBetweenMaps);

            int next = currentMapIndex + 1;
            if (next >= mapPrefabs.Length)
            {
                if (loopMaps)
                {
                    next = 0;
                }
                else
                {
                    Debug.Log("LevelManager: All maps complete.");
                    IsTransitioning = false;
                    yield break;
                }
            }

            LoadMap(next);
        }

        private void HandleLevelLost()
        {
            StartCoroutine(RetryAfterPause());
        }

        private IEnumerator RetryAfterPause()
        {
            IsTransitioning = true;

            if (ballChainManager != null)
                ballChainManager.SetMoving(false);

            yield return new WaitForSeconds(pauseBetweenMaps);

            LoadMap(currentMapIndex);
        }
    }
}
