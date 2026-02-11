using UnityEngine;
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
        [SerializeField] private Transform spawnPoint;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnCooldown = 0.5f;
        [SerializeField] private int initialPoolSize = 20;
        [SerializeField] private int colorCount = 4;

        [Header("Test Mode")]
        [SerializeField] private bool randomColors = true;
        [SerializeField] private BallColor fixedColor = BallColor.Red;
        [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;

        private ObjectPool<Projectile> projectilePool;
        private float lastSpawnTime;
        private BallColor nextColor;

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
            bool shouldShoot = false;

            #if UNITY_EDITOR || UNITY_STANDALONE
            // Mouse input for editor and standalone
            shouldShoot = Input.GetMouseButtonDown(0);
            #else
            // Touch input for mobile
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                shouldShoot = touch.phase == TouchPhase.Began;
            }
            #endif

            // Alternative keyboard input for testing
            if (Input.GetKeyDown(shootKey) && shootKey != KeyCode.Mouse0)
            {
                shouldShoot = true;
            }

            if (shouldShoot)
            {
                TrySpawnProjectile();
            }
        }

        /// <summary>
        /// Attempts to spawn a projectile if cooldown has elapsed.
        /// </summary>
        private void TrySpawnProjectile()
        {
            if (Time.time - lastSpawnTime < spawnCooldown)
            {
                return; // Still on cooldown
            }

            SpawnProjectile();
            lastSpawnTime = Time.time;
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
            projectile.Initialize(color, ballChainManager);
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
                int colorIndex = Random.Range(0, colorCount);
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
            }
        }

        /// <summary>
        /// Sets the number of different colors to use.
        /// </summary>
        public void SetColorCount(int count)
        {
            colorCount = Mathf.Clamp(count, 1, 6);
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
