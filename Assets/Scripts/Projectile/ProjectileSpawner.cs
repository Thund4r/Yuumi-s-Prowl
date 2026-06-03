using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private GameManager gameManager;

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
        // All projectiles currently in flight. With loose homing the player can have
        // many at once; without it the list holds at most one.
        private readonly List<Projectile> inFlightProjectiles = new List<Projectile>(8);

        /// <summary>
        /// Multi-fire mode: when loose homing is unlocked, the player can fire without
        /// waiting for the previous projectile to land.
        /// </summary>
        private bool MultiFireEnabled => runtimeStats != null && runtimeStats.HomingLooseEnabled;

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

            if (gameManager != null)
            {
                gameManager.OnGameWon  += HandleLevelEnded;
                gameManager.OnGameLost += HandleLevelEnded;
            }

            // Refill the loaded projectile after each level's intro animation finishes.
            // Combined with ClearActiveProjectiles NOT respawning, this guarantees that
            // when a run ends (no next intro fires) the player cannot shoot during the
            // EndRun → main-menu pause.
            if (ballSpawner != null)
                ballSpawner.OnIntroComplete += HandleIntroComplete;

            // Keep projectile colours honest as the chain's colour set changes (matches clearing
            // a colour should re-roll a loaded projectile of that now-absent colour).
            if (matchProcessor != null)
                matchProcessor.OnBallsDestroyed += HandleBallsDestroyed;

            // Initialize next color
            nextColor = GetNextColor();
            SpawnNextProjectile();
        }

        private void HandleIntroComplete()
        {
            // Only refill if a prior level-end cleared us out. On the very first intro
            // (or after a shop node, which doesn't fire OnGameWon/OnGameLost) the
            // currentProjectile is still loaded — no-op then.
            if (currentProjectile == null)
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
            float bombRad = runtimeStats != null ? runtimeStats.ExplosionRadius
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

            #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
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

            // Effective cooldown = base spawnCooldown minus FireRate bonuses from purple
            // synergy upgrades. Clamped to a small minimum so we can't fire faster than once
            // per ~50ms.
            float fireBonus = runtimeStats != null ? runtimeStats.FireRateBonus : 0f;
            float effectiveCooldown = Mathf.Max(0.05f, spawnCooldown - fireBonus);
            if (Time.time - lastSpawnTime < effectiveCooldown) return;

            // Note: shooting during match processing / gap closing is allowed. The match
            // processor supports concurrent sequences, so a projectile-induced match in any
            // segment will be processed even while another sequence is still in progress.

            // Single-fire mode: gate on whether any projectile is in flight.
            // Multi-fire (loose homing): allow firing regardless of in-flight count —
            // the cooldown above is the only rate limit.
            if (!MultiFireEnabled && inFlightProjectiles.Count > 0) return;

            Projectile launched = currentProjectile;
            lastSpawnTime = Time.time;

            // Consume equipped power-up before launch so that if the projectile
            // completes instantly (e.g. Pierce), the next spawned projectile
            // won't inherit the consumed power-up.
            if (powerUpInventory != null && powerUpInventory.EquippedPowerUp != PowerUpType.None)
                powerUpInventory.ConsumeEquipped();

            // Refresh homing config from RuntimeStats — this projectile may have been
            // spawned before the player drafted a homing upgrade, in which case its
            // SetHoming-at-spawn values are stale.
            if (runtimeStats != null)
            {
                launched.SetHoming(
                    runtimeStats.HomingStrictEnabled,
                    runtimeStats.HomingLooseEnabled,
                    runtimeStats.HomingRange);
            }

            launched.Launch(targetWorldPos);

            // If the projectile completed instantly (e.g. Pierce), ReturnProjectile
            // already ran during Launch and respawned the next projectile. Don't override.
            if (currentProjectile == launched)
            {
                inFlightProjectiles.Add(launched);
                currentProjectile = null;

                // Multi-fire: immediately ready the next projectile so the player can
                // keep shooting. Single-fire: defer to ReturnProjectile when the in-flight
                // one returns (preserves the existing "one at a time" feel).
                if (MultiFireEnabled)
                    SpawnNextProjectile();
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

            // Pass per-run homing config from RuntimeStats — disabled by default.
            if (runtimeStats != null)
            {
                currentProjectile.SetHoming(
                    runtimeStats.HomingStrictEnabled,
                    runtimeStats.HomingLooseEnabled,
                    runtimeStats.HomingRange);
            }
            else
            {
                currentProjectile.SetHoming(false, false, 0f);
            }
        }

        /// <summary>
        /// Picks the next projectile colour. Uses the same `RuntimeStats.ColorWeights`
        /// as the ball spawner (so projectile colours follow the same intended
        /// distribution as the chain), but zeroes out the weight of any colour that
        /// isn't currently present in the chain. Net effect:
        ///   - A colour that's been completely cleared from the chain stops generating
        ///     projectiles until tail spawns put one back.
        ///   - The mix of remaining projectile colours matches the spawn-weight ratio
        ///     of the colours still in play (not the chain's instantaneous composition).
        /// If every colour is absent (empty chain), falls through to uniform pick so the
        /// next-projectile preview always has something to show.
        /// </summary>
        private BallColor GetNextColor()
        {
            if (!randomColors) return fixedColor;

            int maxColors = (ballSpawner != null) ? ballSpawner.ColorCount : 4;
            if (maxColors <= 1) return (BallColor)0;

            // Start from the spawn weights — same source the ball spawner uses for tail balls.
            float[] weights = new float[maxColors];
            for (int i = 0; i < maxColors; i++)
                weights[i] = runtimeStats != null ? runtimeStats.GetColorWeight((BallColor)i) : 1f;

            // Mark which colours are currently in the chain.
            if (ballChainManager != null)
            {
                bool[] present = new bool[maxColors];
                var segments = ballChainManager.GetSegments();
                if (segments != null)
                {
                    for (int s = 0; s < segments.Count; s++)
                    {
                        var seg = segments[s];
                        for (int i = 0; i < seg.balls.Count; i++)
                        {
                            var node = seg.balls[i];
                            if (node == null || node.ball == null) continue;
                            int idx = (int)node.ball.BallColor;
                            if (idx >= 0 && idx < maxColors) present[idx] = true;
                        }
                    }
                }

                // Gate the spawn weights by chain presence. Absent colours go to 0.
                for (int i = 0; i < maxColors; i++)
                    if (!present[i]) weights[i] = 0f;
            }

            // PickWeightedColor falls back to uniform if every weight is 0 (e.g. empty
            // chain during a level transition), so no further fallback needed here.
            return BallColorUtils.PickWeightedColor(maxColors, weights);
        }

        /// <summary>
        /// When a match clears balls, keep projectile colours in sync with the chain: refresh the
        /// upcoming colour, and re-roll the *loaded* projectile if its colour is no longer present
        /// anywhere in the chain (so the player never holds a colour that isn't in play). Deferred
        /// one frame so the match's removals have fully settled before we test presence.
        /// </summary>
        private void HandleBallsDestroyed(int count, BallColor color)
        {
            if (!randomColors || !isActiveAndEnabled) return;
            StartCoroutine(RefreshColorsNextFrame());
        }

        private IEnumerator RefreshColorsNextFrame()
        {
            yield return null;
            nextColor = GetNextColor();
            if (currentProjectile != null && !IsColorInChain(currentProjectile.ProjectileColor))
                currentProjectile.SetColor(GetNextColor());
        }

        /// <summary>True if any visible ball in the chain currently has the given colour.</summary>
        private bool IsColorInChain(BallColor color)
        {
            if (ballChainManager == null) return true;
            var segments = ballChainManager.GetSegments();
            if (segments == null) return false;
            for (int s = 0; s < segments.Count; s++)
            {
                var seg = segments[s];
                for (int i = 0; i < seg.balls.Count; i++)
                {
                    var node = seg.balls[i];
                    if (node?.ball == null) continue;
                    if (!node.ball.gameObject.activeInHierarchy) continue;
                    if (node.ball.BallColor == color) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a projectile to the pool.
        /// Called by the projectile itself when it hits a ball.
        /// </summary>
        public void ReturnProjectile(Projectile projectile)
        {
            if (projectile == null) return;

            bool wasInFlight = inFlightProjectiles.Remove(projectile);
            // Instant power-ups (Pierce) call this during Launch, before TryLaunchProjectile
            // has moved the projectile into the in-flight list — so it's still currentProjectile.
            bool wasCurrent = currentProjectile == projectile;

            // If the projectile is neither tracked as in-flight nor as the loaded one, it
            // has already been handled (e.g. ClearActiveProjectiles raced an instant-Pierce
            // return on level-end). Ignore — returning it again would double-pool it.
            if (!wasInFlight && !wasCurrent) return;

            projectilePool.Return(projectile);
            projectile.OnReturnToPool();

            if (wasCurrent) currentProjectile = null;

            // Refill if nothing is loaded. Single-fire: this is the refill moment.
            // Multi-fire: SpawnNextProjectile already ran at launch, so this is a no-op.
            if (currentProjectile == null)
                SpawnNextProjectile();
        }

        /// <summary>
        /// Forces any in-flight or loaded projectile back to the pool. Called on
        /// GameManager.OnGameWon / OnGameLost so a shot launched right before a level
        /// ends can't carry over and insert into the next level's chain mid-intro.
        ///
        /// Deliberately does NOT spawn a replacement — that happens via HandleIntroComplete
        /// after the next level's intro plays. On the final-run win there is no next intro,
        /// so the player has no loaded shot during the EndRun → main-menu pause.
        /// </summary>
        public void ClearActiveProjectiles()
        {
            // Return every in-flight projectile (multi-fire can have several at once).
            for (int i = 0; i < inFlightProjectiles.Count; i++)
            {
                var p = inFlightProjectiles[i];
                if (p == null) continue;
                projectilePool.Return(p);
                p.OnReturnToPool();
            }
            inFlightProjectiles.Clear();

            if (currentProjectile != null)
            {
                projectilePool.Return(currentProjectile);
                currentProjectile.OnReturnToPool();
                currentProjectile = null;
            }
        }

        private void HandleLevelEnded()
        {
            ClearActiveProjectiles();
        }

        private void OnDestroy()
        {
            if (powerUpInventory != null)
                powerUpInventory.OnPowerUpEquipped -= HandlePowerUpEquipped;

            if (gameManager != null)
            {
                gameManager.OnGameWon  -= HandleLevelEnded;
                gameManager.OnGameLost -= HandleLevelEnded;
            }

            if (ballSpawner != null)
                ballSpawner.OnIntroComplete -= HandleIntroComplete;

            if (matchProcessor != null)
                matchProcessor.OnBallsDestroyed -= HandleBallsDestroyed;

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
