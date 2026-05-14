using UnityEngine;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Configuration for a single meta upgrade (cost, ranks, progression).
    /// </summary>
    [System.Serializable]
    public class MetaUpgradeConfig
    {
        public string upgradeId; // "ChargePerBall", "EssenceGain", "BallSpeedReduction", "DraftReroll"
        public string upgradeName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("Progression")]
        [Tooltip("Number of ranks/stages for this upgrade.")]
        [Min(1)] public int maxRanks = 6;

        [Tooltip("Fixed essence cost per rank.")]
        [Min(1)] public int essenceCostPerRank = 50;

        [Header("Stat Progression")]
        [Tooltip("Per-rank increment. For Charge: additive (e.g., 1.67 per rank). For Essence Gain: additive multiplier (e.g., 0.167 per rank). For Speed: additive (e.g., 0.083 per rank = -8.3%).")]
        public float incrementPerRank = 1f;

        [Tooltip("Final cap value (max value after all ranks).")]
        public float capValue = 10f;
    }

    /// <summary>
    /// Tunable configuration for meta progression (essence rewards, meta upgrade caps, etc.).
    /// Create assets via: Right-click in Project → Yuumi → Meta Progression Settings
    /// </summary>
    [CreateAssetMenu(fileName = "MetaProgressionSettings", menuName = "Yuumi/Meta Progression Settings")]
    public class MetaProgressionSettings : ScriptableObject
    {
        [Header("Essence Rewards")]
        [Tooltip("Base essence awarded per floor cleared.")]
        [Min(1)] public int baseEssencePerFloor = 10;

        [Tooltip("Curve multiplier applied to essence based on floor progress (0 at first, 1 at last). Empty = constant 1.0.")]
        public AnimationCurve essenceDepthCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("If enabled, essence is multiplied by (ballSpeedMult * totalBallsMult) for difficulty scaling.")]
        public bool essenceDifficultyScaling = true;

        [Header("Meta Upgrades")]
        [Tooltip("Definitions of all purchasable meta upgrades.")]
        public MetaUpgradeConfig[] metaUpgrades = new MetaUpgradeConfig[0];

        /// <summary>
        /// Safe sampler for the essence depth curve.
        /// Returns 1.0 if the curve is empty (unconfigured).
        /// </summary>
        public float SampleEssenceDepthMult(float t)
        {
            if (essenceDepthCurve == null || essenceDepthCurve.length == 0)
                return 1f;
            return essenceDepthCurve.Evaluate(Mathf.Clamp01(t));
        }

        /// <summary>
        /// Looks up a meta upgrade config by ID.
        /// </summary>
        public MetaUpgradeConfig GetUpgradeConfig(string upgradeId)
        {
            if (metaUpgrades == null)
                return null;

            foreach (var cfg in metaUpgrades)
            {
                if (cfg.upgradeId == upgradeId)
                    return cfg;
            }
            return null;
        }

        /// <summary>
        /// Calculates the current value of an upgrade based on rank (0-based).
        /// </summary>
        public float GetUpgradeValue(string upgradeId, int rank)
        {
            var cfg = GetUpgradeConfig(upgradeId);
            if (cfg == null)
                return 0f;

            // Clamp rank to valid range
            rank = Mathf.Clamp(rank, 0, cfg.maxRanks);

            // For additive upgrades: value = incrementPerRank * (rank + 1)
            // For multiplicative (EssenceGain): value = 1.0 + incrementPerRank * (rank + 1)
            if (upgradeId == "EssenceGain")
                return 1f + cfg.incrementPerRank * (rank + 1);

            return cfg.incrementPerRank * (rank + 1);
        }
    }
}
