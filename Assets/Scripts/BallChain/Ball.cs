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

        // Runtime properties
        private float pathProgress;
        private int chainIndex;
        private Material ballMaterial;

        public BallColor BallColor => ballColor;
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
        /// Updates the ball's visual appearance based on its color.
        /// </summary>
        private void UpdateVisuals()
        {
            if (ballMaterial == null && meshRenderer != null)
            {
                ballMaterial = meshRenderer.material;
            }

            if (ballMaterial == null) return;

            // Set material color based on ball color
            ballMaterial.color = BallColorUtils.ToUnityColor(ballColor);
        }

        /// <summary>
        /// Resets the ball for object pooling.
        /// </summary>
        public void ResetBall()
        {
            pathProgress = 0f;
            chainIndex = -1;
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
