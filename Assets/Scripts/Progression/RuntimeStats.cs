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

        [Header("Explosion")]
        [Tooltip("Radius of explosions — used by the Bomb power-up AND red-match explosions. " +
                 "Count-scaled by RunManager from the number of red synergy upgrades owned; not an upgrade card.")]
        public float ExplosionRadius;
        [Tooltip("If true, red matches that meet the size threshold trigger an explosion.")]
        public bool RedMatchExplosionEnabled;
        [Tooltip("Lowers the red-match explosion size threshold (floored at 3). Additive from upgrades.")]
        public int ExplosionThresholdReduction;
        [Tooltip("Weight of Bomb vs Pierce when PowerUpChargeTracker awards a power-up. Pierce is always 1.0; higher values bias toward Bomb. Baseline 1.0 = 50/50.")]
        public float BombAwardWeight;

        [Header("Ice Patches (Blue)")]
        [Tooltip("If true, blue matches drop ice patches that frost-stack passing balls, and destroyed frozen balls spawn icicles. Set by the IcePatches anchor upgrade.")]
        public bool IcePatchesEnabled;
        [Tooltip("If true, blue matches also emit an AoE freezing ring that adds +1 frost stack to balls in range. Prereq: IcePatchesEnabled.")]
        public bool CryoBurstEnabled;
        [Tooltip("If true, destroyed frozen balls emit a cryo burst (in addition to spawning an icicle). Prereq: CryoBurstEnabled.")]
        public bool CryoBurstChainEnabled;
        [Tooltip("If true, icicles prefer already-frozen balls when one is available; falls back to random untargeted otherwise.")]
        public bool FreezeTheHuntedEnabled;
        [Tooltip("Subtracted from RunConfig.iceFreezeStackThreshold (floored at 1). Each rank lowers the stacks-to-freeze count.")]
        public int FrostThresholdReduction;
        [Tooltip("If true, every blue synergy upgrade owned (including this one) slows the chain after blue matches. Magnitude scales by ColorSynergyCounts[Blue].")]
        public bool BlueChainSlowdownEnabled;
        [Tooltip("Additive seconds added to the chain-slowdown window after a blue match (on top of RunConfig.blueSlowdownBaseDuration).")]
        public float BlueSlowdownDurationBonus;

        [Header("Rage Synergy (Purple)")]
        [Tooltip("If true, the rage meter is unlocked. Set by the RageUnlock 'anchor' purple upgrade.")]
        public bool RageEnabled;
        [Tooltip("Additive bonus to rage gained per ball destroyed (on top of RunConfig.rageGainPerBall).")]
        public float RageBuildupBonus;
        [Tooltip("Additive seconds added to the rage active duration (on top of RunConfig.rageDuration).")]
        public float RageDurationBonus;
        [Tooltip("Additive seconds removed from the projectile spawn cooldown — faster firing.")]
        public float FireRateBonus;

        [Header("Cached per-floor")]
        [Tooltip("Count of colour-synergy upgrades owned, indexed by BallColor. Populated by RunManager.RecomputeSynergyStats() each floor load.")]
        public int[] ColorSynergyCounts;

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

        public void SetColorWeight(BallColor color, float value)
        {
            int i = (int)color;
            if (ColorWeights != null && i >= 0 && i < ColorWeights.Length)
                ColorWeights[i] = Mathf.Max(0f, value);
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

        public int GetColorSynergyCount(BallColor color)
        {
            int i = (int)color;
            return (ColorSynergyCounts != null && i >= 0 && i < ColorSynergyCounts.Length) ? ColorSynergyCounts[i] : 0;
        }

        public void SetColorSynergyCount(BallColor color, int count)
        {
            int i = (int)color;
            if (ColorSynergyCounts != null && i >= 0 && i < ColorSynergyCounts.Length)
                ColorSynergyCounts[i] = count;
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
            RedMatchExplosionEnabled = false;
            ExplosionThresholdReduction = 0;
            BombAwardWeight = 1f;
            IcePatchesEnabled = false;
            CryoBurstEnabled = false;
            CryoBurstChainEnabled = false;
            FreezeTheHuntedEnabled = false;
            FrostThresholdReduction = 0;
            BlueChainSlowdownEnabled = false;
            BlueSlowdownDurationBonus = 0f;
            RageEnabled = false;
            RageBuildupBonus = 0f;
            RageDurationBonus = 0f;
            FireRateBonus = 0f;
            ColorSynergyCounts = new int[ColorCount];

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
                ExplosionRadius = defaults.bombRadius;
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
                ExplosionRadius = 0f;
                HammerRecoilDistance = 0f;
                Debug.LogWarning("RuntimeStats: Defaults (PowerUpSettings) not assigned — using fallback baselines. Assign PowerUpSettings in the inspector for correct values.");
            }
        }
    }
}
