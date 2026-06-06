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
        [Tooltip("Child GameObject (a flame/ember icon) overlaid on a charging red ball. Its alpha fades in as ignite stacks build, then it blinks once primed and scales up with ignite power. Renderer/sprite must be transparency-capable. Optional — null disables it.")]
        [SerializeField] private GameObject igniteSymbol;
        [Tooltip("Alpha the symbol gains per ignite stack while charging (before primed). Tune ~1/igniteThreshold so it's near full at the prime point.")]
        [Range(0f, 1f)] [SerializeField] private float igniteSymbolAlphaPerStack = 0.34f;
        [Tooltip("Blink cycles per second once the ball is primed.")]
        [Min(0.1f)] [SerializeField] private float ignitePulseSpeed = 3f;
        [Tooltip("Extra symbol scale per ignite power beyond 1, so a higher-power primed ball reads bigger. Clamped by Ignite Symbol Max Scale.")]
        [Min(0f)] [SerializeField] private float igniteSymbolScalePerPower = 0.3f;
        [Tooltip("Maximum symbol scale multiplier, so extreme ignite power can't make it absurdly large.")]
        [Min(1f)] [SerializeField] private float igniteSymbolMaxScale = 3f;

        [Header("Enemy Visuals (Disruptors)")]
        [Tooltip("Base colour a Stone enemy paints over the ball (colourless wall). Stones aren't colour-matchable.")]
        [SerializeField] private Color stoneColor = new Color(0.5f, 0.5f, 0.52f, 1f);
        [Tooltip("Symbol to indicate a ball is a warden.")]
        [SerializeField] private GameObject wardenSymbol;
        [Tooltip("Colour a Warden's base ball is tinted toward (keeps its colour readable for matching, but reads as menacing).")]
        [SerializeField] private Color wardenTint = new Color(0.12f, 0f, 0.18f, 1f);
        [Tooltip("How strongly the Warden tint is blended over the ball's colour. 0 = no change, 1 = fully wardenTint.")]
        [Range(0f, 1f)] [SerializeField] private float wardenTintStrength = 0.4f;

        // Cached renderers + instanced materials on the frozen overlay so we can drive
        // their alpha at runtime without mutating the shared material asset.
        private Renderer[] frostOverlayRenderers;
        private Material[] frostOverlayMaterials;
        private float[] frostOverlayBaseAlphas;
        private Color[] frostOverlayBaseColors;

        // Ignite symbol caches — per-renderer base colour + (for non-sprite renderers) an instanced
        // material, so we can drive alpha for both SpriteRenderers and mesh/quad renderers.
        private Renderer[] igniteSymbolRenderers;
        private Material[] igniteSymbolMaterials;
        private Color[] igniteSymbolBaseColors;
        private Vector3 igniteSymbolBaseScale = Vector3.one;
        private Coroutine ignitePulseRoutine;

        // Runtime properties
        private float pathProgress;
        private int chainIndex;
        private MaterialPropertyBlock ballPropertyBlock;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private BallPowerUpType powerUpType = BallPowerUpType.None;
        private float powerUpValue = 0f;
        private int frostStacks = 0;
        private bool frozen = false;
        private int igniteStacks = 0;
        private bool primed = false;
        private int ignitePower = 1;
        private EnemyType enemyType = EnemyType.None;

        public BallColor BallColor => ballColor;
        public BallPowerUpType PowerUpType => powerUpType;
        public EnemyType EnemyType => enemyType;
        /// <summary>
        /// True if this ball can take part in a colour match. False for power-up balls and for
        /// Stone enemies (colourless walls). Wardens are coloured, so they remain matchable.
        /// </summary>
        public bool IsColorMatchable => powerUpType == BallPowerUpType.None && enemyType != EnemyType.Stone;
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

            // All balls share one material; per-ball colour is supplied via a
            // MaterialPropertyBlock (SetBallColor) so they can be GPU-instanced into a single
            // draw call. Creating a per-ball material here would break that.
            ballPropertyBlock = new MaterialPropertyBlock();
            if (meshRenderer != null && meshRenderer.sharedMaterial == null)
            {
                Debug.LogWarning($"Ball '{name}' MeshRenderer has no material assigned — assign one in the prefab's Inspector.", this);
            }

            CacheFrostOverlayMaterials();
            CacheIgniteSymbol();
            UpdateFrostOverlay();
        }

        private void OnEnable()
        {
            // A queue ball can be deactivated (below the hole) and reactivated while staying in
            // the chain; Unity stops coroutines on deactivate, so re-apply the ignite symbol state
            // here — restart the blink if primed, otherwise the fade-in if it was charging.
            if (primed) UpdateIgnitePrimedSymbol();
            else if (igniteStacks > 0) UpdateIgniteSymbolCharging();
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
        /// Captures the ignite symbol's renderers and (for non-sprite renderers) instances their
        /// materials, so its alpha can be driven without mutating shared assets. Records the base
        /// local scale so the primed power-scaling can multiply from it.
        /// </summary>
        private void CacheIgniteSymbol()
        {
            if (igniteSymbol == null) return;
            igniteSymbolBaseScale = igniteSymbol.transform.localScale;
            igniteSymbolRenderers = igniteSymbol.GetComponentsInChildren<Renderer>(includeInactive: true);
            igniteSymbolMaterials = new Material[igniteSymbolRenderers.Length];
            igniteSymbolBaseColors = new Color[igniteSymbolRenderers.Length];
            for (int i = 0; i < igniteSymbolRenderers.Length; i++)
            {
                if (igniteSymbolRenderers[i] is SpriteRenderer sr)
                {
                    igniteSymbolBaseColors[i] = sr.color;   // sprites tint via SpriteRenderer.color
                    igniteSymbolMaterials[i] = null;
                }
                else
                {
                    var src = igniteSymbolRenderers[i].sharedMaterial;
                    if (src == null) { igniteSymbolMaterials[i] = null; continue; }
                    igniteSymbolBaseColors[i] = src.color;
                    var instance = new Material(src);
                    igniteSymbolRenderers[i].material = instance;
                    igniteSymbolMaterials[i] = instance;
                }
            }
            igniteSymbol.SetActive(false);
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
        /// Tags this ball as an enemy disruptor (or clears it). Drives the base-colour
        /// override (grey Stone / tinted Warden) and matchability. Call after Initialize().
        /// </summary>
        public void SetEnemyType(EnemyType type)
        {
            enemyType = type;
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

        /// <summary>Sets the ball's ignite-stack count (Orange Conductor). Drives the ember tint + the fade-in symbol.</summary>
        public void SetIgniteStacks(int stacks)
        {
            igniteStacks = Mathf.Max(0, stacks);
            UpdateVisuals();
            if (!primed) UpdateIgniteSymbolCharging();
        }

        /// <summary>
        /// Marks the ball primed (ignite threshold reached): snaps to the full ember tint, and the
        /// ignite symbol starts blinking, scaled up by ignite power so a higher-power ball reads bigger.
        /// </summary>
        public void SetPrimed(bool isPrimed, int power = 1)
        {
            primed = isPrimed;
            ignitePower = Mathf.Max(1, power);
            UpdateVisuals();
            UpdateIgnitePrimedSymbol();
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
            if (meshRenderer == null || !gameObject.activeInHierarchy) return;
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
                SetBallColor(Color.Lerp(resting, Color.white, t / half));
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                SetBallColor(Color.Lerp(Color.white, resting, t / half));
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
            if (meshRenderer == null) return;

            if (powerUpType == BallPowerUpType.Hammer)
            {
                // Golden color so the hammer ball is immediately distinct
                SetBallColor(new Color(1f, 0.85f, 0.1f));
                return;
            }

            if (enemyType == EnemyType.Stone)
            {
                // Colourless wall — paint it grey so it reads as inert stone, not a colour.
                SetBallColor(stoneColor);
                return;
            }

            Color resting = ApplyIgniteTint(BallColorUtils.ToUnityColor(ballColor));
            if (enemyType == EnemyType.Warden)
                resting = Color.Lerp(resting, wardenTint, wardenTintStrength);
                wardenSymbol.SetActive(enemyType == EnemyType.Warden);
            SetBallColor(resting);
        }

        /// <summary>
        /// Drives this ball's colour through a MaterialPropertyBlock instead of its own material
        /// instance, so every ball keeps sharing one material and Unity can draw them all in a
        /// single GPU-instanced draw call. Setting meshRenderer.material.color would give each
        /// ball a unique material and break instancing.
        /// </summary>
        private void SetBallColor(Color color)
        {
            if (meshRenderer == null) return;
            if (ballPropertyBlock == null) ballPropertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(ballPropertyBlock);
            ballPropertyBlock.SetColor(ColorPropertyId, color);
            meshRenderer.SetPropertyBlock(ballPropertyBlock);
        }

        /// <summary>Blends the base colour toward primedTint by ignite progress (full when primed).</summary>
        private Color ApplyIgniteTint(Color baseColor)
        {
            if (primed) return primedTint;
            if (igniteStacks > 0)
                return Color.Lerp(baseColor, primedTint, Mathf.Clamp01(igniteStacks * igniteTintPerStack));
            return baseColor;
        }

        // ============================================================
        // Ignite symbol — fade-in while charging, blink + power-scale when primed
        // ============================================================

        /// <summary>Charging (not yet primed): the symbol's alpha tracks the ignite stacks.</summary>
        private void UpdateIgniteSymbolCharging()
        {
            if (igniteSymbol == null) return;
            if (igniteStacks > 0)
            {
                igniteSymbol.SetActive(true);
                SetIgniteSymbolAlpha(igniteStacks * igniteSymbolAlphaPerStack);
            }
            else
            {
                igniteSymbol.SetActive(false);
            }
        }

        /// <summary>Primed: scale the symbol by ignite power and start (or stop) the blink.</summary>
        private void UpdateIgnitePrimedSymbol()
        {
            if (igniteSymbol == null) return;

            if (ignitePulseRoutine != null) { StopCoroutine(ignitePulseRoutine); ignitePulseRoutine = null; }

            if (primed)
            {
                float mult = Mathf.Min(igniteSymbolMaxScale, 1f + (ignitePower - 1) * igniteSymbolScalePerPower);
                igniteSymbol.transform.localScale = igniteSymbolBaseScale * mult;
                igniteSymbol.SetActive(true);
                if (gameObject.activeInHierarchy)
                    ignitePulseRoutine = StartCoroutine(IgnitePulseRoutine());
            }
            else
            {
                igniteSymbol.transform.localScale = igniteSymbolBaseScale;
                UpdateIgniteSymbolCharging();   // revert to the fade-in (or hidden)
            }
        }

        private System.Collections.IEnumerator IgnitePulseRoutine()
        {
            while (primed)
            {
                float a = Mathf.Sin(Time.time * ignitePulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
                SetIgniteSymbolAlpha(a);
                yield return null;
            }
        }

        private void SetIgniteSymbolAlpha(float factor)
        {
            if (igniteSymbolRenderers == null) return;
            factor = Mathf.Clamp01(factor);
            for (int i = 0; i < igniteSymbolRenderers.Length; i++)
            {
                Color c = igniteSymbolBaseColors[i];
                c.a = igniteSymbolBaseColors[i].a * factor;
                if (igniteSymbolRenderers[i] is SpriteRenderer sr)
                    sr.color = c;
                else if (igniteSymbolMaterials[i] != null)
                    igniteSymbolMaterials[i].color = c;
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
            frostStacks = 0;
            frozen = false;
            igniteStacks = 0;
            primed = false;
            ignitePower = 1;
            enemyType = EnemyType.None;
            if (ignitePulseRoutine != null) { StopCoroutine(ignitePulseRoutine); ignitePulseRoutine = null; }
            if (powerUpIndicator != null)
                powerUpIndicator.SetActive(false);
            if (frozenOverlay != null)
                frozenOverlay.SetActive(false);
            if (igniteSymbol != null)
            {
                igniteSymbol.transform.localScale = igniteSymbolBaseScale;
                igniteSymbol.SetActive(false);
            }
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
        }

        private void OnDestroy()
        {
            if (frostOverlayMaterials != null)
            {
                for (int i = 0; i < frostOverlayMaterials.Length; i++)
                {
                    if (frostOverlayMaterials[i] != null)
                        Destroy(frostOverlayMaterials[i]);
                }
            }

            if (igniteSymbolMaterials != null)
            {
                for (int i = 0; i < igniteSymbolMaterials.Length; i++)
                {
                    if (igniteSymbolMaterials[i] != null)
                        Destroy(igniteSymbolMaterials[i]);
                }
            }
        }
    }
}
