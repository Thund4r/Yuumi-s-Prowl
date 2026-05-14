using UnityEngine;
using UnityEngine.EventSystems;
using YuumisProwl;
using YuumisProwl.BallChain;
using YuumisProwl.Managers;
using YuumisProwl.PowerUps;
using YuumisProwl.Progression;
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
        [SerializeField] private LevelManager levelManager;

        [Header("Power-Ups")]
        [SerializeField] private PowerUpInventory powerUpInventory;
        [SerializeField] private PowerUpSettings powerUpSettings;
        [Tooltip("Per-run mutable stats. When assigned, overrides pierce/bomb tunables.")]
        [SerializeField] private RuntimeStats runtimeStats;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnCooldown = 0.5f;
        [SerializeField] private int initialPoolSize = 20;
        // Color count is sourced from the canonical BallSpawner when available

        [Header("Test Mode")]
        [SerializeField] private bool randomColors = true;
        [SerializeField] private BallColor fixedColor = BallColor.Red;
        [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;

        // Fired each time a projectile is successfully launched. CannonController subscribes to this.
        public System.Action OnShot;

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

            if (powerUpInventory != null)
                powerUpInventory.OnPowerUpEquipped += HandlePowerUpEquipped;

            // Initialize next color
            nextColor = GetNextColor();
            SpawnNextProjectile();
        }

        private void HandlePowerUpEquipped(PowerUpType type)
        {
            // Update the currently-loaded projectile's visuals/behavior to match.
            // If no projectile is currently loaded (one is in flight), the next spawn
            // will pick up the equipped power-up from the inventory.
            if (currentProjectile != null)
                ApplyEquippedPowerUp(currentProjectile);
        }

        private void ApplyEquippedPowerUp(Projectile projectile)
        {
            if (projectile == null) return;
            if (powerUpInventory == null) { projectile.SetPowerUp(PowerUpType.None); return; }

            PowerUpType type = powerUpInventory.EquippedPowerUp;
            float pierceDist = runtimeStats != null ? runtimeStats.PierceMaxDistance
                             : powerUpSettings != null ? powerUpSettings.pierceMaxDistance
                             : 30f;
            float speedMult = runtimeStats != null ? runtimeStats.PierceSpeedMultiplier
                            : powerUpSettings != null ? powerUpSettings.pierceSpeedMultiplier
                            : 2f;
            float bombRad = runtimeStats != null ? runtimeStats.BombRadius
                          : powerUpSettings != null ? powerUpSettings.bombRadius
                          : 3f;
            float pierceWidth = runtimeStats != null ? runtimeStats.PierceWidthMultiplier : 1f;
            projectile.SetPowerUp(type, pierceDist, speedMult, bombRad, pierceWidth);
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
            if (levelManager != null && levelManager.IsTransitioning) return;

            // Don't shoot when clicking on UI elements
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

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
                // Don't shoot when tapping on UI elements
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    return;
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

            // Note: shooting during match processing / gap closing is allowed. The match
            // processor supports concurrent sequences, so a projectile-induced match in any
            // segment will be processed even while another sequence is still in progress.

            // Prevent firing if a projectile is already in flight
            if (projectileInFlight) return;

            Projectile launched = currentProjectile;
            lastSpawnTime = Time.time;

            // Consume equipped power-up before launch so that if the projectile
            // completes instantly (e.g. Pierce), the next spawned projectile
            // won't inherit the consumed power-up.
            if (powerUpInventory != null && powerUpInventory.EquippedPowerUp != PowerUpType.None)
                powerUpInventory.ConsumeEquipped();

            launched.Launch(targetWorldPos);

            // If the projectile completed instantly (e.g. Pierce), ReturnProjectile
            // was already called during Launch, which reset projectileInFlight and
            // spawned the next projectile. Don't override that state.
            if (currentProjectile == launched)
            {
                projectileInFlight = true;
                currentProjectile = null;
            }

            OnShot?.Invoke();
        }

        private void SpawnNextProjectile()
        {
            Projectile projectile = projectilePool.Get();
            if (projectile == null) return;

            projectile.transform.position = spawnPoint.position;
            projectile.transform.rotation = spawnPoint.rotation;

            BallColor color = randomColors ? nextColor : fixedColor;
            projectile.Initialize(color, ballChainManager, this, matchProcessor);
            projectile.OnGetFromPool();

            currentProjectile = projectile;
            nextColor = GetNextColor();

            // If a power-up is already equipped (e.g. equipped while a prior shot was
            // in flight), apply it to this freshly-loaded projectile.
            ApplyEquippedPowerUp(currentProjectile);
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

        private void OnDestroy()
        {
            if (powerUpInventory != null)
                powerUpInventory.OnPowerUpEquipped -= HandlePowerUpEquipped;

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
