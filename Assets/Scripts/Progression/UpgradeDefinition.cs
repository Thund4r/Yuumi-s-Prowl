using UnityEngine;
using YuumisProwl.PowerUps;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Defines a single in-run upgrade: which stat it modifies and by how much.
    /// Upgrades are ScriptableObject assets created via the asset menu.
    /// </summary>
    public enum UpgradeStat
    {
        YuumiRotationSpeed,
        ChargePerBall,
        PierceWidth,
        BombRadius,
    }

    [CreateAssetMenu(fileName = "Upgrade_", menuName = "Yuumi/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        [field: SerializeField] public UpgradeStat Stat { get; private set; }
        [field: SerializeField] public string UpgradeName { get; private set; }
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }

        [Tooltip("Absolute modifier applied to the stat. For multipliers, use 1.2 = 20% increase.")]
        [field: SerializeField] public float ModifierValue { get; private set; }

        /// <summary>
        /// Applies this upgrade to the given RuntimeStats.
        /// </summary>
        public void Apply(RuntimeStats stats)
        {
            if (stats == null) return;

            switch (Stat)
            {
                case UpgradeStat.YuumiRotationSpeed:
                    stats.YuumiRotationSpeed *= ModifierValue;
                    break;

                case UpgradeStat.ChargePerBall:
                    stats.ChargePerBallDestroyed += Mathf.RoundToInt(ModifierValue);
                    break;

                case UpgradeStat.PierceWidth:
                    stats.PierceWidthMultiplier *= ModifierValue;
                    break;

                case UpgradeStat.BombRadius:
                    stats.BombRadius += ModifierValue;
                    break;
            }
        }
    }
}
