using UnityEngine;
using YuumisProwl;
using YuumisProwl.BallChain;

namespace YuumisProwl.Projectile
{
    /// <summary>
    /// Yuumi's homing projectile that follows player's touch/mouse input.
    /// Inserts into ball chain on collision.
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
        private ProjectileSpawner ownerSpawner;
        private Material projectileMaterial;
        private bool isLaunched;
        public BallColor ProjectileColor => projectileColor;

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
        public void Initialize(BallColor color, BallChainManager chainManager, ProjectileSpawner spawner = null)
        {
            projectileColor = color;
            ballChainManager = chainManager;
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
        /// Updates the projectile's visual appearance based on its color.
        /// </summary>
        private void UpdateVisuals()
        {
            if (projectileMaterial == null && meshRenderer != null)
            {
                projectileMaterial = meshRenderer.material;
            }

            if (projectileMaterial != null)
            {
                projectileMaterial.color = BallColorUtils.ToUnityColor(projectileColor);
            }

            // Update trail color
            if (trailRenderer != null)
            {
                Color trailColor = BallColorUtils.ToUnityColor(projectileColor);
                trailRenderer.startColor = trailColor;
                trailRenderer.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            }
        }

        /// <summary>
        /// Handles collision with balls in the chain and with obstacles.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!isActive) return;

            if (other.CompareTag("Ball"))
            {
                Ball hitBall = other.GetComponent<Ball>();
                if (hitBall != null && ballChainManager != null)
                {
                    float insertProgress = hitBall.PathProgress;
                    ballChainManager.InsertBallAtProgress(projectileColor, insertProgress);

                    Debug.Log($"Projectile hit ball! Inserting {projectileColor} at progress {insertProgress:F2}");

                    if (ownerSpawner != null)
                        ownerSpawner.ReturnProjectile(this);
                    else
                        Deactivate();
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
