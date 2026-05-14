using UnityEngine;
using YuumisProwl.Progression;
using YuumisProwl.Projectile;

namespace YuumisProwl.Player
{
    /// <summary>
    /// Controls Yuumi's rotation to face the player's cursor or touch,
    /// and triggers her throw animation when a projectile is fired.
    ///
    /// Setup:
    ///   1. Place this on Yuumi's GameObject.
    ///   2. Assign the Animator and ProjectileSpawner in the Inspector.
    ///   3. Set Throw Trigger to match the trigger parameter name in your Animator.
    ///   4. Adjust Rotation Offset so Yuumi faces the cursor correctly.
    ///      e.g. if she faces up (+Y) in the sprite, set Rotation Offset to -90.
    /// </summary>
    public class YuumiController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ProjectileSpawner projectileSpawner;
        [SerializeField] private Animator animator;

        [Header("Rotation")]
        [Tooltip("How fast Yuumi tracks the cursor, in degrees per second. 0 = instant snap. " +
                 "Used as a fallback if RuntimeStats is not wired.")]
        [SerializeField] private float rotationSpeed = 720f;
        [Tooltip("Angle offset applied after aiming. Adjust to match your sprite's orientation.")]
        [SerializeField] private float rotationOffset = 0f;

        [Header("Progression")]
        [Tooltip("Per-run mutable stats. When assigned, YuumiRotationSpeed overrides the value above.")]
        [SerializeField] private RuntimeStats runtimeStats;

        [Header("Animation")]
        [Tooltip("The Animator trigger parameter name for the throw animation.")]
        [SerializeField] private string throwTrigger = "Throw";

        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Start()
        {
            if (projectileSpawner == null)
                projectileSpawner = FindObjectOfType<ProjectileSpawner>();

            if (projectileSpawner != null)
                projectileSpawner.OnShot += PlayThrowAnimation;
            else
                Debug.LogWarning("YuumiController: ProjectileSpawner not found.");
        }

        private void OnDestroy()
        {
            if (projectileSpawner != null)
                projectileSpawner.OnShot -= PlayThrowAnimation;
        }

        private void Update()
        {
            RotateTowardsAim();
        }

        /// <summary>
        /// Rotates Yuumi to face the current cursor or touch position.
        /// </summary>
        private void RotateTowardsAim()
        {
            if (mainCamera == null) return;

            Vector3 aimWorld = GetAimWorldPosition();
            Vector3 direction = aimWorld - transform.position;
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.001f) return;

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
            float effectiveSpeed = runtimeStats != null ? runtimeStats.YuumiRotationSpeed : rotationSpeed;

            if (effectiveSpeed <= 0f)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
            }
            else
            {
                float currentAngle = transform.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, effectiveSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }
        }

        private Vector3 GetAimWorldPosition()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            return mainCamera.ScreenToWorldPoint(mousePos);
#else
            if (Input.touchCount > 0)
            {
                Vector3 touchPos = Input.GetTouch(0).position;
                touchPos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
                return mainCamera.ScreenToWorldPoint(touchPos);
            }
            return transform.position;
#endif
        }

        /// <summary>
        /// Triggers Yuumi's throw animation. Called by ProjectileSpawner.OnShot.
        /// </summary>
        private void PlayThrowAnimation()
        {
            if (animator != null)
                animator.SetTrigger(throwTrigger);
        }
    }
}
