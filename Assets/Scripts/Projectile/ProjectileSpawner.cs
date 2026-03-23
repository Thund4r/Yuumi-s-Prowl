using UnityEngine;
using YuumisProwl;
using YuumisProwl.BallChain;
using YuumisProwl.Utilities;

namespace YuumisProwl.Projectile
{
    /// <summary>
    /// Spawns projectiles based on player input.
    /// Manages projectile pooling for performance.
    /// </summary>
    public class ProjectileSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private BallSpawner ballSpawner;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnCooldown = 0.5f;
        [SerializeField] private int initialPoolSize = 20;
        // Color count is sourced from the canonical BallSpawner when available

        [Header("Test Mode")]
        [SerializeField] private bool randomColors = true;
        [SerializeField] private BallColor fixedColor = BallColor.Red;
        [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;

        private ObjectPool<Projectile> projectilePool;
        private float lastSpawnTime;
        private BallColor nextColor;
        private Projectile currentProjectile;
        private bool projectileInFlight = false;

        private void Awake()
        {
            InitializePool();

            if (spawnPoint == null)
            {
                // Create default spawn point
                GameObject spawnObj = new GameObject("ProjectileSpawnPoint");
                spawnObj.transform.SetParent(transform);
                spawnObj.transform.localPosition = new Vector3(0, 0, -5);
                spawnPoint = spawnObj.transform;
            }
        }

        private void Start()
        {
            if (ballChainManager == null)
            {
                Debug.LogError("ProjectileSpawner: BallChainManager not assigned!");
                enabled = false;
                return;
            }

            if (projectilePrefab == null)
            {
                Debug.LogError("ProjectileSpawner: Projectile prefab not assigned!");
                enabled = false;
                return;
            }

            // Initialize next color
            nextColor = GetNextColor();
            SpawnNextProjectile();
        }

        private void Update()
        {
            HandleInput();
        }

        private void InitializePool()
        {
            if (projectilePrefab == null)
            {
                Debug.LogError("ProjectileSpawner: Projectile prefab not assigned!");
                return;
            }

            projectilePool = new ObjectPool<Projectile>(
                projectilePrefab,
                initialPoolSize,
                transform
            );
        }

        /// <summary>
        /// Handles player input for shooting projectiles.
        /// </summary>
        private void HandleInput()
        {
            if (ballSpawner != null && ballSpawner.IsPlayingIntro) return;

            bool shouldShoot = false;
            Vector3 worldTarget = Vector3.zero;
            Camera cam = Camera.main;

            #if UNITY_EDITOR || UNITY_STANDALONE
            shouldShoot = Input.GetMouseButtonDown(0);
            if (shouldShoot && cam != null)
            {
                Vector3 mousePos = Input.mousePosition;
                mousePos.z = Mathf.Abs(cam.transform.position.z - spawnPoint.position.z);
                worldTarget = cam.ScreenToWorldPoint(mousePos);
            }
            #else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                shouldShoot = touch.phase == TouchPhase.Began;
                if (shouldShoot && cam != null)
                {
                    Vector3 touchPos = touch.position;
                    touchPos.z = Mathf.Abs(cam.transform.position.z - spawnPoint.position.z);
                    worldTarget = cam.ScreenToWorldPoint(touchPos);
                }
            }
            #endif

            if (Input.GetKeyDown(shootKey) && shootKey != KeyCode.Mouse0)
            {
                shouldShoot = true;
                if (cam != null)
                {
                    Vector3 mousePos = Input.mousePosition;
                    mousePos.z = Mathf.Abs(cam.transform.position.z - spawnPoint.position.z);
                    worldTarget = cam.ScreenToWorldPoint(mousePos);
                }
            }

            if (shouldShoot)
            {
                TryLaunchProjectile(worldTarget);
            }
        }

        /// <summary>
        /// Attempts to launch the projectile if cooldown has elapsed.
        /// </summary>
        private void TryLaunchProjectile(Vector3 targetWorldPos)
        {
            if (currentProjectile == null) return;
            if (Time.time - lastSpawnTime < spawnCooldown) return;

            // Prevent shooting while the chain is processing matches (including recoil)
            if (matchProcessor != null && matchProcessor.IsProcessingMatches) return;

            // Prevent firing if a projectile is already in flight
            if (projectileInFlight) return;

            currentProjectile.Launch(targetWorldPos);
            projectileInFlight = true;
            currentProjectile = null;
            lastSpawnTime = Time.time;
        }

        private void SpawnNextProjectile()
        {
            Projectile projectile = projectilePool.Get();
            if (projectile == null) return;

            projectile.transform.position = spawnPoint.position;
            projectile.transform.rotation = spawnPoint.rotation;

            BallColor color = randomColors ? nextColor : fixedColor;
            projectile.Initialize(color, ballChainManager, this);
            projectile.OnGetFromPool();

            currentProjectile = projectile;
            nextColor = GetNextColor();
        }

        /// <summary>
        /// Spawns a projectile from the pool.
        /// </summary>
        private void SpawnProjectile()
        {
            Projectile projectile = projectilePool.Get();

            if (projectile == null)
            {
                Debug.LogWarning("Failed to get projectile from pool!");
                return;
            }

            // Position at spawn point
            projectile.transform.position = spawnPoint.position;
            projectile.transform.rotation = spawnPoint.rotation;

            // Initialize with color and chain manager reference
            BallColor color = randomColors ? nextColor : fixedColor;
            projectile.Initialize(color, ballChainManager, this);
            projectile.OnGetFromPool();

            // Get next color for the following projectile
            nextColor = GetNextColor();

            Debug.Log($"Spawned projectile - Color: {color}");
        }

        /// <summary>
        /// Gets the next color for projectiles.
        /// </summary>
        private BallColor GetNextColor()
        {
            if (randomColors)
            {
                int maxColors = (ballSpawner != null) ? ballSpawner.ColorCount : 4;
                int colorIndex = Random.Range(0, maxColors);
                return (BallColor)colorIndex;
            }
            return fixedColor;
        }

        /// <summary>
        /// Returns a projectile to the pool.
        /// Called by the projectile itself when it hits a ball.
        /// </summary>
        public void ReturnProjectile(Projectile projectile)
        {
            if (projectile != null)
            {
                projectilePool.Return(projectile);
                projectile.OnReturnToPool();

                // Mark that the projectile is no longer in flight and prepare the next one
                projectileInFlight = false;
                SpawnNextProjectile();
            }
        }

        /// <summary>
        /// Sets the number of different colors to use.
        /// </summary>
        public void SetColorCount(int count)
        {
            if (ballSpawner != null)
            {
                ballSpawner.SetColorCount(count);
            }
        }

        /// <summary>
        /// Sets the spawn cooldown time.
        /// </summary>
        public void SetCooldown(float cooldown)
        {
            spawnCooldown = Mathf.Max(0f, cooldown);
        }

        private void OnDestroy()
        {
            if (projectilePool != null)
            {
                projectilePool.Clear();
            }
        }

        private void OnDrawGizmos()
        {
            if (spawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(spawnPoint.position, 0.3f);
                Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + spawnPoint.forward * 2f);
            }
        }
    }
}
