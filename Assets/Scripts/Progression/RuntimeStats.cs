using UnityEngine;
using YuumisProwl.PowerUps;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Mutable per-run wrapper for tunables that progression upgrades modify.
    /// Initialized from PowerUpSettings + serialized baselines on Awake; upgrades mutate
    /// the public fields directly so source assets (PowerUpSettings) stay clean.
    ///
    /// Setup:
    ///   1. Add an empty GameObject "RuntimeStats" to the Game scene.
    ///   2. Attach this component.
    ///   3. Drag the shared PowerUpSettings asset into the Defaults slot.
    ///   4. Wire this GameObject into the runtimeStats slot on YuumiController,
    ///      PowerUpChargeTracker, ProjectileSpawner, and PowerUpSpawner.
    ///
    /// Consumers fall back to their existing direct reads when this reference is null,
    /// so the scene wiring can be done incrementally.
    /// </summary>
    public class RuntimeStats : MonoBehaviour
    {
        [Header("Baselines")]
        [Tooltip("Source of default values for charge / power-up tunables. Copied into the fields below on Awake.")]
        [SerializeField] private PowerUpSettings defaults;

        [Tooltip("Default Yuumi rotation speed before upgrades. Should match YuumiController's serialized rotationSpeed.")]
        [SerializeField] private float yuumiRotationSpeedDefault = 720f;

        [Tooltip("Default homing detection radius (world units) before upgrades. Used whenever a homing flag is taken without HomingRange upgrades.")]
        [SerializeField] private float homingRangeBaseline = 5f;

        [Header("Yuumi")]
        public float YuumiRotationSpeed;

        [Header("Charge")]
        public int ChargePerBallDestroyed;
        public int CascadeBonusCharge;
        public int ChargeThreshold;

        [Header("Pierce")]
        public float PierceMaxDistance;
        public float PierceSpeedMultiplier;
        public float PierceWidthMultiplier;

        [Header("Bomb")]
        public float BombRadius;

        [Header("Hammer")]
        public float HammerRecoilDistance;

        [Header("Gold")]
        [Tooltip("Multiplier on gold rewards (1.0 = no bonus, 1.5 = +50%).")]
        public float GoldGainMultiplier;
        [Tooltip("Flat bonus gold per cascade beyond the first match in a sequence.")]
        public int GoldPerCascade;
        [Tooltip("If true, the in-run shop offers a reroll button (cost defined in RunConfig).")]
        public bool ShopRerollEnabled;

        [Header("Meta-driven (set by meta upgrades at run start)")]
        [Tooltip("Multiplier on essence earned at run end (1.0 = no bonus).")]
        public float EssenceGainMultiplier;
        [Tooltip("Amount subtracted from the ball-speed multiplier each floor (0 = no reduction).")]
        public float BallSpeedReduction;
        [Tooltip("Number of free draft rerolls available per level draft.")]
        public int DraftRerollCount;
        [Tooltip("Gold the run begins with (RunState.gold is initialized to this at run start).")]
        public int StartingGold;

        [Header("Color Synergy")]
        [Tooltip("Per-color spawn weight, indexed by BallColor. 1.0 = baseline; higher = more common. Rebuilt every run.")]
        public float[] ColorWeights;
        [Tooltip("Bonus gold granted each time a match of that color is destroyed, indexed by BallColor.")]
        public int[] ColorMatchGold;

        [Header("Homing")]
        [Tooltip("If true, in-flight projectiles auto-target a same-color ball that already has a same-color neighbor (guaranteed 3+ match).")]
        public bool HomingStrictEnabled;
        [Tooltip("If true, projectiles auto-target ANY same-color ball within range (no neighbor required). Subsumes strict mode.")]
        public bool HomingLooseEnabled;
        [Tooltip("Maximum world-space distance from the projectile at which a homing target can be acquired.")]
        public float HomingRange;

        /// <summary>Number of entries in the BallColor enum.</summary>
        private static readonly int ColorCount = System.Enum.GetValues(typeof(BallColor)).Length;

        private void Awake()
        {
            ResetToDefaults();
        }

        // --- Color synergy accessors (bounds-safe) ---

        public float GetColorWeight(BallColor color)
        {
            int i = (int)color;
            return (ColorWeights != null && i >= 0 && i < ColorWeights.Length) ? ColorWeights[i] : 1f;
        }

        public void AddColorWeight(BallColor color, float amount)
        {
            int i = (int)color;
            if (ColorWeights != null && i >= 0 && i < ColorWeights.Length)
                ColorWeights[i] = Mathf.Max(0f, ColorWeights[i] + amount);
        }

        public int GetColorMatchGold(BallColor color)
        {
            int i = (int)color;
            return (ColorMatchGold != null && i >= 0 && i < ColorMatchGold.Length) ? ColorMatchGold[i] : 0;
        }

        public void AddColorMatchGold(BallColor color, int amount)
        {
            int i = (int)color;
            if (ColorMatchGold != null && i >= 0 && i < ColorMatchGold.Length)
                ColorMatchGold[i] += amount;
        }

        /// <summary>
        /// Copies baseline values back into every field. Called by Awake and intended
        /// to be called by a future RunManager at run start (before applying upgrades).
        /// </summary>
        public void ResetToDefaults()
        {
            YuumiRotationSpeed = yuumiRotationSpeedDefault;
            PierceWidthMultiplier = 1f;
            GoldGainMultiplier = 1f;
            GoldPerCascade = 0;
            ShopRerollEnabled = false;
            EssenceGainMultiplier = 1f;
            BallSpeedReduction = 0f;
            DraftRerollCount = 0;
            StartingGold = 0;

            // Rebuild color-synergy arrays — weights baseline 1.0, match-gold baseline 0.
            ColorWeights = new float[ColorCount];
            ColorMatchGold = new int[ColorCount];
            for (int i = 0; i < ColorCount; i++)
                ColorWeights[i] = 1f;

            // Homing — flags disabled until an upgrade enables them; range starts at the
            // serialized baseline so taking a homing flag alone is immediately useful.
            HomingStrictEnabled = false;
            HomingLooseEnabled = false;
            HomingRange = homingRangeBaseline;

            if (defaults != null)
            {
                ChargePerBallDestroyed = defaults.chargePerBallDestroyed;
                CascadeBonusCharge = defaults.cascadeBonusCharge;
                ChargeThreshold = defaults.chargeThreshold;
                PierceMaxDistance = defaults.pierceMaxDistance;
                PierceSpeedMultiplier = defaults.pierceSpeedMultiplier;
                BombRadius = defaults.bombRadius;
                HammerRecoilDistance = defaults.hammerRecoilDistance;
            }
            else
            {
                // No baseline asset — zero the gameplay stats so a missing reference
                // can never leave per-run upgrade values stale across runs.
                ChargePerBallDestroyed = 0;
                CascadeBonusCharge = 0;
                ChargeThreshold = 10;
                PierceMaxDistance = 0f;
                PierceSpeedMultiplier = 1f;
                BombRadius = 0f;
                HammerRecoilDistance = 0f;
                Debug.LogWarning("RuntimeStats: Defaults (PowerUpSettings) not assigned — using fallback baselines. Assign PowerUpSettings in the inspector for correct values.");
            }
        }
    }
}
