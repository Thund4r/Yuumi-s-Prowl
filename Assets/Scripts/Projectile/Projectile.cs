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
        /// Updates the target position based on player input.
        /// </summary>
        private void UpdateTarget()
        {
            if (mainCamera == null) return;

            #if UNITY_EDITOR || UNITY_STANDALONE
            // Always track mouse position in editor/standalone so the
            // projectile continuously homes to the cursor and can circle it.
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            targetPosition = mainCamera.ScreenToWorldPoint(mousePos);
            
            #else
            // Touch input for mobile
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                Vector3 touchPos = touch.position;
                touchPos.z = Mathf.Abs(mainCamera.transform.position.z);
                targetPosition = mainCamera.ScreenToWorldPoint(touchPos);
            }
            #endif
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
                        float recoilDistance = hammer.PowerUpValue;

                        ballChainManager.RemoveBallAtIndex(hammerIndex);

                        // Use ProcessHammerAftermath so the gap-close triggers cascade
                        // detection at the merge boundary and applies a final recoil
                        // — same flow as a normal match.
                        if (matchProcessor != null)
                            matchProcessor.ProcessHammerAftermath(hammerIndex, recoilDistance);

                        Debug.Log($"Hammer triggered! Recoil distance: {recoilDistance}");

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
