using UnityEngine;
using YuumisProwl;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Represents a single ball in the chain.
    /// Holds color information and provides pooling support.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(SphereCollider))]
    public class Ball : MonoBehaviour
    {
        [Header("Ball Properties")]
        [SerializeField] private BallColor ballColor;

        [Header("Visual Settings")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private SphereCollider sphereCollider;

        [Header("Power-Up")]
        [Tooltip("Optional child GameObject shown when this ball is a power-up (e.g. a hammer icon sprite).")]
        [SerializeField] private GameObject powerUpIndicator;

        // Runtime properties
        private float pathProgress;
        private int chainIndex;
        private Material ballMaterial;
        private BallPowerUpType powerUpType = BallPowerUpType.None;
        private float powerUpValue = 0f;

        public BallColor BallColor => ballColor;
        public BallPowerUpType PowerUpType => powerUpType;
        /// <summary>
        /// Generic numeric payload for the active power-up.
        /// For Hammer: the recoil distance in world units.
        /// </summary>
        public float PowerUpValue => powerUpValue;
        public float PathProgress
        {
            get => pathProgress;
            set => pathProgress = value;
        }
        public int ChainIndex
        {
            get => chainIndex;
            set => chainIndex = value;
        }

        private void Awake()
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
            if (sphereCollider == null)
                sphereCollider = GetComponent<SphereCollider>();

            // Create instance material to avoid shared material modification
            if (meshRenderer != null)
            {
                ballMaterial = new Material(meshRenderer.sharedMaterial);
                meshRenderer.material = ballMaterial;
            }
        }

        /// <summary>
        /// Initializes the ball with a specific color.
        /// </summary>
        public void Initialize(BallColor color)
        {
            ballColor = color;
            UpdateVisuals();
        }

        /// <summary>
        /// Marks this ball as a power-up of the given type, storing an optional numeric payload.
        /// Call after Initialize() so the visual override takes effect.
        /// </summary>
        public void SetAsPowerUp(BallPowerUpType type, float value = 0f)
        {
            powerUpType = type;
            powerUpValue = value;
            if (powerUpIndicator != null)
                powerUpIndicator.SetActive(type != BallPowerUpType.None);
            UpdateVisuals();
        }

        /// <summary>
        /// Updates the ball's visual appearance.
        /// Power-up balls override the standard color with a fixed highlight color.
        /// </summary>
        private void UpdateVisuals()
        {
            if (ballMaterial == null && meshRenderer != null)
            {
                ballMaterial = meshRenderer.material;
            }

            if (ballMaterial == null) return;

            if (powerUpType == BallPowerUpType.Hammer)
            {
                // Golden color so the hammer ball is immediately distinct
                ballMaterial.color = new Color(1f, 0.85f, 0.1f);
            }
            else
            {
                ballMaterial.color = BallColorUtils.ToUnityColor(ballColor);
            }
        }

        /// <summary>
        /// Resets the ball for object pooling.
        /// </summary>
        public void ResetBall()
        {
            pathProgress = 0f;
            chainIndex = -1;
            powerUpType = BallPowerUpType.None;
            powerUpValue = 0f;
            if (powerUpIndicator != null)
                powerUpIndicator.SetActive(false);
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// Called when the ball is returned to the pool.
        /// </summary>
        public void OnReturnToPool()
        {
            ResetBall();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Called when the ball is retrieved from the pool.
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

            UpdateVisuals();
        }

        private void OnDestroy()
        {
            // Clean up instanced material
            if (ballMaterial != null)
            {
                Destroy(ballMaterial);
            }
        }
    }
}
