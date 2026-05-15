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

        private void Awake()
        {
            ResetToDefaults();
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
