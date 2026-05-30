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
        [Tooltip("Child GameObject (ice-block prefab) overlaid on the ball. Its renderer material's alpha is driven by frost stacks; on freeze the colour swaps to frozenColor. Material MUST use a transparency-capable shader.")]
        [SerializeField] private GameObject frozenOverlay;
        [Tooltip("Per-stack fraction of the prefab material's authored alpha. e.g. 0.1 = stack 1 shows at 10% of the prefab alpha, stack 2 at 20%, frozen snaps to 100%.")]
        [Range(0f, 1f)] [SerializeField] private float frostAlphaPerStack = 0.1f;
        [Tooltip("Colour the overlay swaps to once the ball is fully frozen. Replaces the prefab's authored colour for the duration of the frozen state.")]
        [SerializeField] private Color frozenColor = new Color(0.5f, 0.85f, 1f, 1f);

        [Header("Ignite Visuals (Orange Conductor)")]
        [Tooltip("Ember tint a ball lerps toward as it accrues ignite stacks; snaps to full once primed. Tints the base material directly (red→hot-orange reads as 'heating up').")]
        [SerializeField] private Color primedTint = new Color(1f, 0.55f, 0.05f, 1f);
        [Tooltip("Per-stack fraction of the lerp toward primedTint before the ball is primed.")]
        [Range(0f, 1f)] [SerializeField] private float igniteTintPerStack = 0.25f;

        // Cached renderers + instanced materials on the frozen overlay so we can drive
        // their alpha at runtime without mutating the shared material asset.
        private Renderer[] frostOverlayRenderers;
        private Material[] frostOverlayMaterials;
        private float[] frostOverlayBaseAlphas;
        private Color[] frostOverlayBaseColors;

        // Runtime properties
        private float pathProgress;
        private int chainIndex;
        private Material ballMaterial;
        private BallPowerUpType powerUpType = BallPowerUpType.None;
        private float powerUpValue = 0f;
        private int frostStacks = 0;
        private bool frozen = false;
        private int igniteStacks = 0;
        private bool primed = false;

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
            frostOverlayRenderers = frozenOverlay.GetComponentsInChildren<Renderer>(includeInactive: true);
            frostOverlayMaterials = new Material[frostOverlayRenderers.Length];
            frostOverlayBaseAlphas = new float[frostOverlayRenderers.Length];
            frostOverlayBaseColors = new Color[frostOverlayRenderers.Length];
            for (int i = 0; i < frostOverlayRenderers.Length; i++)
            {
                var src = frostOverlayRenderers[i].sharedMaterial;
                if (src == null) { frostOverlayMaterials[i] = null; continue; }
                frostOverlayBaseAlphas[i] = src.color.a;
                frostOverlayBaseColors[i] = src.color;
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
        /// Sets the ball's frost-stack count (Blue ice-patches synergy). Drives the overlay alpha.
        /// </summary>
        public void SetFrostStacks(int stacks)
        {
            frostStacks = Mathf.Max(0, stacks);
            UpdateFrostOverlay();
        }

        /// <summary>
        /// Sets the ball's frozen state. When true the overlay swaps to frozenColor at full opacity.
        /// </summary>
        public void SetFrozen(bool isFrozen)
        {
            frozen = isFrozen;
            if (isFrozen) frostStacks = 0;
            UpdateFrostOverlay();
        }

        /// <summary>Sets the ball's ignite-stack count (Orange Conductor). Drives the ember tint.</summary>
        public void SetIgniteStacks(int stacks)
        {
            igniteStacks = Mathf.Max(0, stacks);
            UpdateVisuals();
        }

        /// <summary>Marks the ball primed (ignite threshold reached) — snaps to the full ember tint.</summary>
        public void SetPrimed(bool isPrimed)
        {
            primed = isPrimed;
            UpdateVisuals();
        }

        /// <summary>
        /// Brief alpha-flash on the frost overlay — used by cryo burst to confirm
        /// "this ball just got hit by the ring." Pulses to full alpha, then restores
        /// the stack-driven alpha when the routine finishes.
        /// </summary>
        public void FlashFrost(float duration = 0.15f)
        {
            if (frozenOverlay == null) return;
            StartCoroutine(FlashFrostRoutine(duration));
        }

        private System.Collections.IEnumerator FlashFrostRoutine(float duration)
        {
            if (frozenOverlay == null) yield break;
            frozenOverlay.SetActive(true);
            SetFrostOverlayAlpha(1f);
            yield return new WaitForSeconds(duration);
            UpdateFrostOverlay();
        }

        /// <summary>
        /// Brief white "zap" flash on the ball's base material — used by the Conductor arc to show
        /// "this ball just got struck." Flashes toward white, then eases back to the resting colour
        /// (base colour + ignite tint).
        /// </summary>
        public void FlashZap(float duration = 0.18f)
        {
            if (ballMaterial == null || !gameObject.activeInHierarchy) return;
            StartCoroutine(FlashZapRoutine(duration));
        }

        private System.Collections.IEnumerator FlashZapRoutine(float duration)
        {
            Color resting = powerUpType == BallPowerUpType.Hammer
                ? new Color(1f, 0.85f, 0.1f)
                : ApplyIgniteTint(BallColorUtils.ToUnityColor(ballColor));

            float half = Mathf.Max(0.01f, duration * 0.5f);
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                if (ballMaterial != null) ballMaterial.color = Color.Lerp(resting, Color.white, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                if (ballMaterial != null) ballMaterial.color = Color.Lerp(Color.white, resting, t / half);
                yield return null;
            }
            UpdateVisuals();
        }

        private void UpdateFrostOverlay()
        {
            if (frozenOverlay == null) return;

            if (frozen)
            {
                frozenOverlay.SetActive(true);
                SetFrostOverlayFrozenColor();
            }
            else if (frostStacks > 0)
            {
                frozenOverlay.SetActive(true);
                float alpha = Mathf.Clamp(frostStacks * frostAlphaPerStack, 0f, 0.99f);
                SetFrostOverlayAlpha(alpha);
            }
            else
            {
                frozenOverlay.SetActive(false);
            }
        }

        private void SetFrostOverlayAlpha(float factor)
        {
            if (frostOverlayMaterials == null) return;
            for (int i = 0; i < frostOverlayMaterials.Length; i++)
            {
                var mat = frostOverlayMaterials[i];
                if (mat == null) continue;
                Color c = frostOverlayBaseColors != null && i < frostOverlayBaseColors.Length
                    ? frostOverlayBaseColors[i]
                    : mat.color;
                c.a = frostOverlayBaseAlphas[i] * factor;
                mat.color = c;
            }
        }

        private void SetFrostOverlayFrozenColor()
        {
            if (frostOverlayMaterials == null) return;
            for (int i = 0; i < frostOverlayMaterials.Length; i++)
            {
                var mat = frostOverlayMaterials[i];
                if (mat == null) continue;
                mat.color = frozenColor;
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

            ballMaterial.color = ApplyIgniteTint(BallColorUtils.ToUnityColor(ballColor));
        }

        /// <summary>Blends the base colour toward primedTint by ignite progress (full when primed).</summary>
        private Color ApplyIgniteTint(Color baseColor)
        {
            if (primed) return primedTint;
            if (igniteStacks > 0)
                return Color.Lerp(baseColor, primedTint, Mathf.Clamp01(igniteStacks * igniteTintPerStack));
            return baseColor;
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
            igniteStacks = 0;
            primed = false;
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
