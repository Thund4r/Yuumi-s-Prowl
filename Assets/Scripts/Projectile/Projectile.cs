using System.Collections.Generic;
using UnityEngine;
using YuumisProwl;
using YuumisProwl.BallChain;
using YuumisProwl.PowerUps;

namespace YuumisProwl.Projectile
{
    /// <summary>
    /// Yuumi's homing projectile that follows player's touch/mouse input.
    /// Inserts into ball chain on collision.
    ///
    /// When an equipped power-up (PowerUpType) is set via SetPowerUp before launch,
    /// the projectile's behavior changes. Current special behaviors:
    ///   Pierce — on launch, instantly raycasts along the firing direction and
    ///            destroys all balls in the line. Cascades process immediately.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(SphereCollider))]
    public class Projectile : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float homingSpeed = 10f;
        [SerializeField] private float rotationSpeed = 5f;
        [Tooltip("When a homing-locked projectile gets this close to its locked ball, it inserts directly — prevents orbiting/missing.")]
        [SerializeField] private float homingArrivalDistance = 0.5f;

        [Header("Visual Settings")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private SphereCollider sphereCollider;
        [SerializeField] private TrailRenderer trailRenderer;

        [Header("Projectile Properties")]
        [SerializeField] private BallColor projectileColor;

        private Vector3 targetPosition;
        private bool isActive;
        private Camera mainCamera;
        private BallChainManager ballChainManager;
        private MatchProcessor matchProcessor;
        private ProjectileSpawner ownerSpawner;
        private Material projectileMaterial;
        private bool isLaunched;

        // Power-up state
        private PowerUpType equippedPowerUp = PowerUpType.None;
        private float pierceMaxDistance;
        private float pierceSpeedMultiplier = 1f;
        private float pierceWidthMultiplier = 1f;
        private float distanceTraveled;
        private int pierceDestroyCount;
        private float bombRadius;

        // Homing state (set per-launch by ProjectileSpawner).
        private bool homingStrictEnabled;
        private bool homingLooseEnabled;
        private float homingRange;
        // The ball this projectile has locked onto. Once set, the projectile commits to
        // it — follows its live position and passes through everything else.
        private Ball homingLock;

        public BallColor ProjectileColor => projectileColor;
        public PowerUpType EquippedPowerUp => equippedPowerUp;

        private void Awake()
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
            if (sphereCollider == null)
                sphereCollider = GetComponent<SphereCollider>();
            if (trailRenderer == null)
                trailRenderer = GetComponent<TrailRenderer>();

            mainCamera = Camera.main;

            // Create instance material
            if (meshRenderer != null)
            {
                projectileMaterial = new Material(meshRenderer.sharedMaterial);
                meshRenderer.material = projectileMaterial;
            }
        }

        public void Launch(Vector3 targetWorldPosition)
        {
            // Set immediate target to the clicked world position
            targetPosition = targetWorldPosition;

            // Face the target immediately so the initial movement
            // goes toward the clicked position, then resume homing.
            Vector3 dir = targetWorldPosition - transform.position;
            dir.z = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            isLaunched = true;

            if (equippedPowerUp == PowerUpType.Pierce)
                ExecutePierceInstant();
        }

        private void Update()
        {
            if (!isActive) return;
            if (!isLaunched) return;

            UpdateTarget();
            MoveTowardsTarget();
            RotateTowardsTarget();
            TryHomingArrival();
        }

        /// <summary>
        /// Initializes the projectile with a color and chain manager reference.
        /// </summary>
        public void Initialize(BallColor color, BallChainManager chainManager, ProjectileSpawner spawner = null, MatchProcessor processor = null)
        {
            projectileColor = color;
            ballChainManager = chainManager;
            matchProcessor = processor;
            isActive = true;
            ownerSpawner = spawner;

            UpdateVisuals();

            // Clear trail
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }
        }

        /// <summary>
        /// Re-colours a loaded (not-yet-launched) projectile — used by ProjectileSpawner when this
        /// projectile's colour has been cleared from the chain, so the player never holds a colour
        /// that isn't in play. (Power-up visuals still override the displayed colour.)
        /// </summary>
        public void SetColor(BallColor color)
        {
            projectileColor = color;
            UpdateVisuals();
        }

        /// <summary>
        /// Equips a power-up on this projectile. Call after Initialize, before Launch.
        /// For Pierce, pierceDistance caps how far the projectile travels before despawning,
        /// speedMultiplier scales its flight speed, and widthMultiplier scales the cast radius.
        /// </summary>
        public void SetPowerUp(PowerUpType type, float pierceDistance = 0f, float speedMultiplier = 1f, float bombExplosionRadius = 0f, float widthMultiplier = 1f)
        {
            equippedPowerUp = type;
            pierceMaxDistance = pierceDistance;
            pierceSpeedMultiplier = speedMultiplier > 0f ? speedMultiplier : 1f;
            pierceWidthMultiplier = widthMultiplier > 0f ? widthMultiplier : 1f;
            distanceTraveled = 0f;
            pierceDestroyCount = 0;
            bombRadius = bombExplosionRadius;
            UpdateVisuals();
        }

        /// <summary>
        /// Configures fire-and-forget homing for this projectile. Called by ProjectileSpawner
        /// at spawn time from the player's RuntimeStats.
        ///   strict — home on same-color balls that have a same-color neighbor (guaranteed match).
        ///   loose  — home on ANY same-color ball (subsumes strict).
        ///   range  — max world-space distance to acquire a target.
        /// </summary>
        public void SetHoming(bool strict, bool loose, float range)
        {
            homingStrictEnabled = strict;
            homingLooseEnabled = loose;
            homingRange = Mathf.Max(0f, range);
        }

        /// <summary>
        /// Updates the target position. Default behavior tracks the cursor / touch.
        /// When homing is enabled (and the player isn't holding right-mouse to override),
        /// the target is overridden to a same-color ball in the chain within homingRange.
        /// </summary>
        private void UpdateTarget()
        {
            if (mainCamera == null) return;

            // Read input — held right-click forces cursor mode and drops any homing lock.
            Vector3 cursorTarget = targetPosition;
            bool homingOverride = false;

            #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            cursorTarget = mainCamera.ScreenToWorldPoint(mousePos);
            homingOverride = Input.GetMouseButton(1); // hold right-click to disable homing
            #else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                Vector3 touchPos = touch.position;
                touchPos.z = Mathf.Abs(mainCamera.transform.position.z);
                cursorTarget = mainCamera.ScreenToWorldPoint(touchPos);
            }
            #endif

            bool homingEnabled = (homingStrictEnabled || homingLooseEnabled)
                                 && homingRange > 0f && ballChainManager != null;

            if (homingOverride || !homingEnabled)
            {
                // Manual control — drop any lock so collision behaves normally.
                homingLock = null;
                targetPosition = cursorTarget;
                return;
            }

            // Keep the current lock while it's valid; otherwise acquire a new one.
            // Once locked, the projectile commits — it follows the ball's live position
            // (tracking chain movement) and passes through everything else.
            if (!IsLockValid())
                homingLock = AcquireHomingTarget();

            targetPosition = homingLock != null ? homingLock.transform.position : cursorTarget;
        }

        /// <summary>
        /// Picks the closest valid same-color ball within homingRange to lock onto.
        /// In strict mode (no loose) the ball must have a same-color neighbor so insertion
        /// guarantees a 3+ match. Power-up balls (hammers) are never targeted. No
        /// line-of-sight test is needed — a locked projectile passes through everything
        /// but its target, so any in-range candidate is reachable.
        /// </summary>
        private Ball AcquireHomingTarget()
        {
            var segments = ballChainManager.GetSegments();
            if (segments == null || segments.Count == 0) return null;

            bool strictOnly = !homingLooseEnabled;
            float bestSq = homingRange * homingRange;
            Ball best = null;
            Vector3 selfPos = transform.position;

            for (int s = 0; s < segments.Count; s++)
            {
                var balls = segments[s].balls;
                for (int i = 0; i < balls.Count; i++)
                {
                    var node = balls[i];
                    if (node.ball == null) continue;
                    if (!node.ball.gameObject.activeInHierarchy) continue; // skip invisible queue balls below the hole
                    if (!node.ball.IsColorMatchable) continue; // skip hammers + non-matchable Stones (Wardens stay targetable)
                    if (node.ball.BallColor != projectileColor) continue;

                    if (strictOnly && !HasSameColorNeighbor(balls, i)) continue;

                    float sq = (node.ball.transform.position - selfPos).sqrMagnitude;
                    if (sq <= bestSq)
                    {
                        bestSq = sq;
                        best = node.ball;
                    }
                }
            }

            return best;
        }

        private bool HasSameColorNeighbor(List<BallNode> balls, int i)
        {
            if (i > 0)
            {
                var n = balls[i - 1].ball;
                if (n != null && n.gameObject.activeInHierarchy && n.IsColorMatchable && n.BallColor == projectileColor)
                    return true;
            }
            if (i < balls.Count - 1)
            {
                var n = balls[i + 1].ball;
                if (n != null && n.gameObject.activeInHierarchy && n.IsColorMatchable && n.BallColor == projectileColor)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True while the locked target is still worth homing onto — active, still the
        /// projectile's color, and not a power-up ball. A destroyed ball is pooled and
        /// deactivated, so activeSelf going false signals the lock should drop.
        /// </summary>
        private bool IsLockValid()
        {
            return homingLock != null
                && homingLock.gameObject.activeSelf
                && homingLock.IsColorMatchable
                && homingLock.BallColor == projectileColor;
        }

        /// <summary>
        /// Safety net: if a homing-locked projectile drifts within homingArrivalDistance
        /// of its target, insert directly instead of waiting for the collider — prevents
        /// a fast projectile orbiting a target it can't quite turn into.
        /// </summary>
        private void TryHomingArrival()
        {
            if (homingLock == null || !isActive) return;

            float sq = (homingLock.transform.position - transform.position).sqrMagnitude;
            if (sq <= homingArrivalDistance * homingArrivalDistance)
                InsertProjectileBall(homingLock);
        }

        /// <summary>
        /// Inserts this projectile's ball into the chain next to the given ball, then
        /// returns the projectile to the pool. Shared by collision and homing-arrival.
        /// </summary>
        private void InsertProjectileBall(Ball ball)
        {
            if (!isActive || ball == null || ballChainManager == null) return;

            ballChainManager.InsertBallAtProgress(projectileColor, ball.PathProgress, transform.position);
            homingLock = null;

            if (ownerSpawner != null)
                ownerSpawner.ReturnProjectile(this);
            else
                Deactivate();
        }

        /// <summary>
        /// Moves the projectile towards the target position.
        /// </summary>
        private void MoveTowardsTarget()
        {
            float angle = transform.eulerAngles.z * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            transform.position += forward * homingSpeed * Time.deltaTime;
        }

        /// <summary>
        /// Rotates the projectile to face the movement direction.
        /// </summary>
        private void RotateTowardsTarget()
        {
            Vector3 direction = targetPosition - transform.position;
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.001f) return;

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.z;

            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }

        /// <summary>
        /// Updates the projectile's visual appearance based on its color or equipped power-up.
        /// </summary>
        private void UpdateVisuals()
        {
            if (projectileMaterial == null && meshRenderer != null)
            {
                projectileMaterial = meshRenderer.material;
            }

            Color baseColor;
            switch (equippedPowerUp)
            {
                case PowerUpType.Pierce: baseColor = Color.white; break;
                case PowerUpType.Bomb:   baseColor = new Color(1f, 0.4f, 0f); break; // orange
                default:                 baseColor = BallColorUtils.ToUnityColor(projectileColor); break;
            }

            if (projectileMaterial != null)
            {
                projectileMaterial.color = baseColor;
            }

            if (trailRenderer != null)
            {
                trailRenderer.startColor = baseColor;
                trailRenderer.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            }
        }

        /// <summary>
        /// Handles collision with balls in the chain and with obstacles.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!isActive) return;

            // Bomb: explode on contact with anything (ball or obstacle)
            if (equippedPowerUp == PowerUpType.Bomb)
            {
                if (other.CompareTag("Ball") || other.GetComponent<Obstacle>() != null)
                    ExecuteBombExplosion();
                return;
            }

            // Homing pass-through: while locked onto a target, ignore every collider that
            // isn't the locked ball — balls drifting into the path and obstacles can't
            // cause a misfire. Only the locked ball triggers insertion.
            if (homingLock != null && other.GetComponent<Ball>() != homingLock)
                return;

            if (other.CompareTag("Ball"))
            {
                Ball hitBall = other.GetComponent<Ball>();
                if (hitBall != null && ballChainManager != null)
                {
                    // Check if the hit ball or either neighbor is a hammer
                    Ball hammer = FindAdjacentHammer(hitBall);

                    if (hammer != null)
                    {
                        int hammerIndex = hammer.ChainIndex;

                        // Removing the hammer fires BallChainManager.OnHammerDestroyed,
                        // which MatchProcessor handles (recoil + cascade aftermath).
                        ballChainManager.RemoveBallAtIndex(hammerIndex);
                        homingLock = null;

                        Debug.Log($"Hammer triggered by projectile at index {hammerIndex}.");

                        if (ownerSpawner != null)
                            ownerSpawner.ReturnProjectile(this);
                        else
                            Deactivate();
                    }
                    else
                    {
                        InsertProjectileBall(hitBall);
                    }
                }
            }
            else if (other.GetComponent<Obstacle>() != null)
            {
                Debug.Log($"Projectile hit obstacle — discarded.");

                if (ownerSpawner != null)
                    ownerSpawner.ReturnProjectile(this);
                else
                    Deactivate();
            }
        }

        /// <summary>
        /// Returns a hammer ball if the hit ball itself is a hammer, or if either
        /// direct neighbor *in the same segment* is a hammer. Neighbors across a
        /// segment gap don't count.
        /// </summary>
        private Ball FindAdjacentHammer(Ball hitBall)
        {
            if (hitBall.PowerUpType == BallPowerUpType.Hammer)
                return hitBall;

            ChainSegment seg = ballChainManager.GetSegmentForChainIndex(hitBall.ChainIndex);
            if (seg == null) return null;

            int local = -1;
            for (int i = 0; i < seg.Count; i++)
            {
                if (seg.balls[i].ball == hitBall) { local = i; break; }
            }
            if (local < 0) return null;

            if (local - 1 >= 0 && seg.balls[local - 1].ball.PowerUpType == BallPowerUpType.Hammer)
                return seg.balls[local - 1].ball;

            if (local + 1 < seg.Count && seg.balls[local + 1].ball.PowerUpType == BallPowerUpType.Hammer)
                return seg.balls[local + 1].ball;

            return null;
        }

        /// <summary>
        /// Explodes at the current position, destroying all balls within bombRadius.
        /// Hands off to MatchProcessor for cascade processing, then returns to pool.
        /// </summary>
        private void ExecuteBombExplosion()
        {
            int removed = ballChainManager != null
                ? ballChainManager.RemoveBallsInRadius(transform.position, bombRadius)
                : 0;

            Debug.Log($"Bomb exploded! Destroyed {removed} balls in radius {bombRadius}.");

            if (removed > 0 && matchProcessor != null)
                matchProcessor.ProcessPierceAftermath(removed);

            if (ownerSpawner != null)
                ownerSpawner.ReturnProjectile(this);
            else
                Deactivate();
        }

        /// <summary>
        /// Executes Pierce instantly on launch: raycasts along the firing direction,
        /// removes all balls in the line, then hands off to MatchProcessor for cascades.
        /// </summary>
        private void ExecutePierceInstant()
        {
            float angle = transform.eulerAngles.z * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float castRadius = (sphereCollider != null ? sphereCollider.radius : 0.3f) * pierceWidthMultiplier;

            RaycastHit[] hits = Physics.SphereCastAll(
                transform.position, castRadius, direction, pierceMaxDistance);

            // Collect chain indices of hit balls
            List<int> indicesToRemove = new List<int>();
            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("Ball"))
                {
                    Ball ball = hit.collider.GetComponent<Ball>();
                    if (ball != null && !indicesToRemove.Contains(ball.ChainIndex))
                        indicesToRemove.Add(ball.ChainIndex);
                }
            }

            // Remove highest indices first so earlier indices stay valid
            indicesToRemove.Sort((a, b) => b.CompareTo(a));

            foreach (int index in indicesToRemove)
            {
                if (ballChainManager != null)
                    ballChainManager.RemoveBallAtIndex(index);
            }

            pierceDestroyCount = indicesToRemove.Count;

            if (matchProcessor != null)
                matchProcessor.ProcessPierceAftermath(pierceDestroyCount);

            if (ownerSpawner != null)
                ownerSpawner.ReturnProjectile(this);
            else
                Deactivate();
        }

        /// <summary>
        /// Deactivates the projectile.
        /// </summary>
        public void Deactivate()
        {
            isActive = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Called when projectile is returned to pool.
        /// </summary>
        public void OnReturnToPool()
        {
            isActive = false;
            isLaunched = false;
            targetPosition = Vector3.zero;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            // Clear power-up state so the next retrieval starts fresh.
            equippedPowerUp = PowerUpType.None;
            pierceMaxDistance = 0f;
            pierceSpeedMultiplier = 1f;
            pierceWidthMultiplier = 1f;
            distanceTraveled = 0f;
            pierceDestroyCount = 0;
            bombRadius = 0f;

            // Clear homing state too.
            homingStrictEnabled = false;
            homingLooseEnabled = false;
            homingRange = 0f;
            homingLock = null;

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Called when projectile is retrieved from pool.
        /// </summary>
        public void OnGetFromPool()
        {
            gameObject.SetActive(true);
        }

        private void OnValidate()
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
            if (sphereCollider == null)
                sphereCollider = GetComponent<SphereCollider>();
            if (trailRenderer == null)
                trailRenderer = GetComponent<TrailRenderer>();
        }

        private void OnDestroy()
        {
            if (projectileMaterial != null)
            {
                Destroy(projectileMaterial);
            }
        }
    }
}
