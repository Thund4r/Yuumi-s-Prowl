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

        [Header("Frost / Frozen Visuals (Blue Ice Patches)")]
        [Tooltip("Child GameObject (ice-block prefab) overlaid on the ball. Its renderer material's alpha and its localScale are driven by frost stacks (partial) / frozen state (full). Material MUST use a transparency-capable shader.")]
        [SerializeField] private GameObject frozenOverlay;
        [Tooltip("Per-stack fraction of the prefab material's authored alpha. e.g. 0.1 = stack 1 shows at 10% of the prefab alpha, stack 2 at 20%, frozen snaps to 100%.")]
        [Range(0f, 1f)] [SerializeField] private float frostAlphaPerStack = 0.1f;
        [Tooltip("Scale (% of prefab's authored localScale) added per frost stack. e.g. 0.33 = stack 1 is 33%, stack 2 is 66%, stack 3 is 100%. Clamped at frozenScale.")]
        [Range(0f, 1f)] [SerializeField] private float frostScalePerStack = 0.33f;
        [Tooltip("Scale when fully frozen, as a fraction of the prefab's authored localScale. 1.0 = full prefab size, and the per-stack scale is clamped to this value.")]
        [Range(0f, 1f)] [SerializeField] private float frozenScale = 1.0f;

        // Cached renderers + instanced materials on the frozen overlay so we can drive
        // their alpha at runtime without mutating the shared material asset.
        private Renderer[] frostOverlayRenderers;
        private Material[] frostOverlayMaterials;
        private float[] frostOverlayBaseAlphas;
        private Vector3 frostOverlayBaseScale = Vector3.one;

        // Runtime properties
        private float pathProgress;
        private int chainIndex;
        private Material ballMaterial;
        private BallPowerUpType powerUpType = BallPowerUpType.None;
        private float powerUpValue = 0f;
        private int frostStacks = 0;
        private bool frozen = false;

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
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                ballMaterial = new Material(meshRenderer.sharedMaterial);
                meshRenderer.material = ballMaterial;
            }
            else if (meshRenderer != null)
            {
                Debug.LogWarning($"Ball '{name}' MeshRenderer has no material assigned — assign one in the prefab's Inspector.", this);
            }

            CacheFrostOverlayMaterials();
            UpdateFrostOverlay();
        }

        /// <summary>
        /// Captures every Renderer under the frozenOverlay child (including inactive ones)
        /// and instances their materials so we can drive their alpha without mutating the
        /// shared material asset.
        /// </summary>
        private void CacheFrostOverlayMaterials()
        {
            if (frozenOverlay == null) return;
            frostOverlayBaseScale = frozenOverlay.transform.localScale;
            frostOverlayRenderers = frozenOverlay.GetComponentsInChildren<Renderer>(includeInactive: true);
            frostOverlayMaterials = new Material[frostOverlayRenderers.Length];
            frostOverlayBaseAlphas = new float[frostOverlayRenderers.Length];
            for (int i = 0; i < frostOverlayRenderers.Length; i++)
            {
                var src = frostOverlayRenderers[i].sharedMaterial;
                if (src == null) { frostOverlayMaterials[i] = null; continue; }
                frostOverlayBaseAlphas[i] = src.color.a;
                var instance = new Material(src);
                frostOverlayRenderers[i].material = instance;
                frostOverlayMaterials[i] = instance;
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
        /// Sets the ball's frost-stack count (Blue ice-patches synergy). Drives the
        /// overlay's alpha and scale.
        /// </summary>
        public void SetFrostStacks(int stacks)
        {
            frostStacks = Mathf.Max(0, stacks);
            UpdateFrostOverlay();
        }

        /// <summary>
        /// Sets the ball's frozen state. When true the overlay snaps to full opacity
        /// and frozenScale. Triggers the icicle hook on destruction (via BallNode).
        /// </summary>
        public void SetFrozen(bool isFrozen)
        {
            frozen = isFrozen;
            if (isFrozen) frostStacks = 0;
            UpdateFrostOverlay();
        }

        /// <summary>
        /// Drives the frozen overlay child's active state, alpha, and scale.
        /// Alpha and scale are both fractions of the prefab's authored values.
        ///   Frozen        → overlay ON,  alpha = prefab,       scale = frozenScale × prefab
        ///   stacks > 0    → overlay ON,  alpha ∝ stacks,       scale grows with stacks
        ///   else          → overlay OFF
        /// </summary>
        private void UpdateFrostOverlay()
        {
            if (frozenOverlay == null) return;

            if (frozen)
            {
                frozenOverlay.SetActive(true);
                SetFrostOverlayAlpha(1f);
                ApplyFrostOverlayScale(frozenScale);
            }
            else if (frostStacks > 0)
            {
                frozenOverlay.SetActive(true);
                float alpha = Mathf.Clamp(frostStacks * frostAlphaPerStack, 0f, 0.99f);
                SetFrostOverlayAlpha(alpha);
                ApplyFrostOverlayScale(Mathf.Clamp(frostStacks * frostScalePerStack, 0f, frozenScale));
            }
            else
            {
                frozenOverlay.SetActive(false);
            }
        }

        private void ApplyFrostOverlayScale(float factor)
        {
            if (frozenOverlay == null) return;
            frozenOverlay.transform.localScale = frostOverlayBaseScale * factor;
        }

        private void SetFrostOverlayAlpha(float factor)
        {
            if (frostOverlayMaterials == null) return;
            for (int i = 0; i < frostOverlayMaterials.Length; i++)
            {
                var mat = frostOverlayMaterials[i];
                if (mat == null) continue;
                Color c = mat.color;
                c.a = frostOverlayBaseAlphas[i] * factor;
                mat.color = c;
            }
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
                return;
            }

            ballMaterial.color = BallColorUtils.ToUnityColor(ballColor);
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
            frostStacks = 0;
            frozen = false;
            if (powerUpIndicator != null)
                powerUpIndicator.SetActive(false);
            if (frozenOverlay != null)
                frozenOverlay.SetActive(false);
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

            if (frostOverlayMaterials != null)
            {
                for (int i = 0; i < frostOverlayMaterials.Length; i++)
                {
                    if (frostOverlayMaterials[i] != null)
                        Destroy(frostOverlayMaterials[i]);
                }
            }
        }
    }
}
