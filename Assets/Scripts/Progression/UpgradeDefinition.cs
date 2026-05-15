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
        GoldGainBonus,    // multiplicative bonus to gold rewards (use 1.2 for +20%)
        GoldPerCascade,   // additive flat gold per cascade match
        ShopReroll,       // enables shop reroll (one-shot, modifier ignored)
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

        [field: Header("Avaliability")]
        [Tooltip("Can appear in the end-of-level upgrade draft.")]
        [field: SerializeField] public bool IsDraftable { get; private set; } = true;

        [Tooltip("Can appear in the in-run shop.")]
        [field: SerializeField] public bool IsShoppable { get; private set; } = false;

        [field: Header("Stackable")]
        [Tooltip("If false, this upgrade can only be acquired once per run. Once owned, it won't appear in future drafts or shops.")]
        [field: SerializeField] public bool IsStackable { get; private set; } = true;

        [field: Header("Shop")]
        [Tooltip("Gold cost to buy this upgrade in the shop. Ignored if IsShoppable is false.")]
        [field: SerializeField] public int ShopCost { get; private set; } = 100;

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

                case UpgradeStat.GoldGainBonus:
                    stats.GoldGainMultiplier *= ModifierValue;
                    break;

                case UpgradeStat.GoldPerCascade:
                    stats.GoldPerCascade += Mathf.RoundToInt(ModifierValue);
                    break;

                case UpgradeStat.ShopReroll:
                    stats.ShopRerollEnabled = true;
                    break;
            }
        }
    }
}
