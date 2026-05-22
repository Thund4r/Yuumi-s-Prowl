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

            // Read input — held right-click forces cursor mode even if homing is on.
            Vector3 cursorTarget = targetPosition;
            bool homingOverride = false;

            #if UNITY_EDITOR || UNITY_STANDALONE
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

            Vector3? homingTarget = null;
            bool homingActive = (homingStrictEnabled || homingLooseEnabled)
                                && homingRange > 0f && !homingOverride
                                && ballChainManager != null;
            if (homingActive)
                homingTarget = FindHomingTarget();

            targetPosition = homingTarget ?? cursorTarget;
        }

        /// <summary>
        /// Scans the chain for the closest same-color ball within homingRange that the
        /// projectile can actually reach without colliding into something that'd cause
        /// a miss. Different-color balls and obstacles between the projectile and the
        /// target disqualify a candidate; same-color blockers are allowed (the projectile
        /// would still match at that ball).
        /// In strict mode (no loose), the candidate must have a same-color neighbor in
        /// the same segment, so insertion guarantees a 3+ match. Power-up balls are ignored.
        /// </summary>
        private Vector3? FindHomingTarget()
        {
            var segments = ballChainManager.GetSegments();
            if (segments == null || segments.Count == 0) return null;

            bool strictOnly = !homingLooseEnabled;
            float bestSq = homingRange * homingRange;
            Vector3? best = null;
            Vector3 selfPos = transform.position;

            for (int s = 0; s < segments.Count; s++)
            {
                var balls = segments[s].balls;
                for (int i = 0; i < balls.Count; i++)
                {
                    var node = balls[i];
                    if (node.ball == null) continue;
                    if (node.ball.PowerUpType != BallPowerUpType.None) continue; // skip hammers etc.
                    if (node.ball.BallColor != projectileColor) continue;

                    if (strictOnly && !HasSameColorNeighbor(balls, i)) continue;

                    Vector3 ballPos = node.ball.transform.position;
                    float sq = (ballPos - selfPos).sqrMagnitude;
                    if (sq > bestSq) continue;

                    // Reject candidates whose path from the projectile is blocked by an
                    // obstacle or a different-color ball — those would cause a missed shot.
                    if (!IsPathClear(node.ball, ballPos)) continue;

                    bestSq = sq;
                    best = ballPos;
                }
            }

            return best;
        }

        private bool HasSameColorNeighbor(List<BallNode> balls, int i)
        {
            if (i > 0)
            {
                var n = balls[i - 1].ball;
                if (n != null && n.PowerUpType == BallPowerUpType.None && n.BallColor == projectileColor)
                    return true;
            }
            if (i < balls.Count - 1)
            {
                var n = balls[i + 1].ball;
                if (n != null && n.PowerUpType == BallPowerUpType.None && n.BallColor == projectileColor)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True if a sphere-cast from the projectile to the given target ball isn't
        /// blocked by anything that would cause a missed shot — different-color balls,
        /// hammer/power-up balls, or obstacles. Same-color blockers are tolerated
        /// because the projectile would still match if it hit one.
        /// </summary>
        private bool IsPathClear(Ball targetBall, Vector3 targetPos)
        {
            Vector3 fromPos = transform.position;
            Vector3 toDir = targetPos - fromPos;
            float dist = toDir.magnitude;
            if (dist < 0.05f) return true;

            Vector3 dir = toDir / dist;
            float radius = sphereCollider != null ? sphereCollider.radius : 0.3f;

            if (Physics.SphereCast(fromPos, radius, dir, out RaycastHit hit, dist))
            {
                Ball blocker = hit.collider.GetComponent<Ball>();
                if (blocker == targetBall) return true;

                // Same-color, non-power-up ball blocker → fine, projectile would still match.
                if (blocker != null
                    && blocker.PowerUpType == BallPowerUpType.None
                    && blocker.BallColor == projectileColor)
                    return true;

                // Anything else in the way (different color, hammer, obstacle) → blocked.
                return false;
            }

            return true;
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

                        Debug.Log($"Hammer triggered by projectile at index {hammerIndex}.");

                        if (ownerSpawner != null)
                            ownerSpawner.ReturnProjectile(this);
                        else
                            Deactivate();
                    }
                    else
                    {
                        float insertProgress = hitBall.PathProgress;
                        ballChainManager.InsertBallAtProgress(projectileColor, insertProgress, transform.position);

                        Debug.Log($"Projectile hit ball! Inserting {projectileColor} at progress {insertProgress:F2}");

                        if (ownerSpawner != null)
                            ownerSpawner.ReturnProjectile(this);
                        else
                            Deactivate();
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
            Collider[] hits = Physics.OverlapSphere(transform.position, bombRadius);

            List<int> indicesToRemove = new List<int>();
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Ball"))
                {
                    Ball ball = hit.GetComponent<Ball>();
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

            Debug.Log($"Bomb exploded! Destroyed {indicesToRemove.Count} balls in radius {bombRadius}.");

            if (matchProcessor != null)
                matchProcessor.ProcessPierceAftermath(indicesToRemove.Count);

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
