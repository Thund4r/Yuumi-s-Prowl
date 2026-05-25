using UnityEngine;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Defines a single upgrade — used uniformly for end-of-level drafts, the in-run
    /// shop, and the meta shop. Each upgrade is its own ScriptableObject asset.
    ///
    /// Availability flags decide which contexts an upgrade appears in. An upgrade can
    /// be acquired up to MaxRank times; every rank adds ModifierValue to the stat
    /// (all stats scale linearly — see UpgradeDefinition.Apply).
    /// </summary>
    public enum UpgradeStat
    {
        YuumiRotationSpeed,   // multiplier field (starts 1.0)
        ChargePerBall,        // raw int
        PierceWidth,          // multiplier field (starts 1.0)
        BombRadius,           // DEPRECATED — explosion radius is count-scaled, not a card. Don't author new cards with this.
        GoldGainBonus,        // multiplier field (starts 1.0)
        GoldPerCascade,       // raw int
        ShopReroll,           // flag — enables the in-run shop reroll button
        EssenceGain,          // multiplier field (starts 1.0)
        BallSpeedReduction,   // raw float — subtracted from the ball-speed multiplier
        DraftReroll,          // raw int — free draft rerolls per level
        StartingGold,         // raw int — gold the run begins with
        ColorWeight,          // DEPRECATED — colour spawn weight is count-scaled from synergy upgrades, not a card.
        ColorMatchGold,       // raw int — gold per match of TargetColor
        HomingStrict,         // DEPRECATED — homing is now rage-gated; the rage meter sets HomingStrictEnabled dynamically.
        HomingLoose,          // DEPRECATED — see above; loose tier is set by the rage meter at high purple count.
        HomingRange,          // raw float — adds to the homing detection radius
        RedMatchExplosion,    // flag — red matches at/above the size threshold trigger an explosion
        ExplosionThresholdReduction, // raw int — lowers the red-match explosion size threshold
        BombSpawnWeight,      // raw float — biases the Bomb vs Pierce roll when a power-up is awarded
        RageBuildupRate,      // raw float — extra rage gained per ball destroyed
        RageDuration,         // raw float — extra seconds added to rage active duration
        FireRate,             // raw float — seconds shaved off the projectile spawn cooldown
        RageUnlock,           // flag — anchor purple upgrade that enables the rage meter
    }

    [CreateAssetMenu(fileName = "Upgrade_", menuName = "Yuumi/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        [field: Header("Identity")]
        [Tooltip("Stable unique ID. REQUIRED for meta upgrades (used as the save key). " +
                 "Must not change once players have saves referencing it.")]
        [field: SerializeField] public string UpgradeId { get; private set; }

        [field: SerializeField] public UpgradeStat Stat { get; private set; }
        [field: SerializeField] public string UpgradeName { get; private set; }
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }

        [Tooltip("Flat per-rank delta — added to the stat once per acquired rank. " +
                 "Multiplier stats (PierceWidth, GoldGainBonus, EssenceGain) start at 1.0, " +
                 "so 0.2 = +20% per rank. Raw stats (ChargePerBall, BombRadius, etc.) take a raw delta. " +
                 "Total at max rank = ModifierValue × MaxRank.")]
        [field: SerializeField] public float ModifierValue { get; private set; }

        [Tooltip("Which ball color this upgrade targets / belongs to. Used by color stats " +
                 "(ColorWeight, ColorMatchGold) and by color-synergy grouping.")]
        [field: SerializeField] public BallColor TargetColor { get; private set; }

        [Tooltip("If true, this is a colour-synergy upgrade — it belongs to TargetColor's synergy, " +
                 "gets a coloured card background, and counts toward that colour's synergy total.")]
        [field: SerializeField] public bool IsColorSynergy { get; private set; }

        [field: Header("Availability")]
        [Tooltip("Can appear in the end-of-level upgrade draft.")]
        [field: SerializeField] public bool IsDraftable { get; private set; } = true;

        [Tooltip("Can appear in the in-run shop (bought with gold).")]
        [field: SerializeField] public bool IsShoppable { get; private set; } = false;

        [Tooltip("Can be bought in the main-menu meta shop (bought with essence, persists across runs).")]
        [field: SerializeField] public bool IsMetaShop { get; private set; } = false;

        [field: Header("Ranks")]
        [Tooltip("Maximum times this upgrade can be acquired. 1 = non-stackable. Higher = stackable/ranked.")]
        [field: SerializeField, Min(1)] public int MaxRank { get; private set; } = 1;

        [field: Header("Costs")]
        [Tooltip("Gold cost in the in-run shop. Ignored if IsShoppable is false.")]
        [field: SerializeField, Min(0)] public int ShopCost { get; private set; } = 100;

        [Tooltip("Essence cost per rank in the meta shop. Index 0 = cost of the 1st purchase, " +
                 "index 1 = 2nd, etc. Length should equal MaxRank. Ignored if IsMetaShop is false.")]
        [field: SerializeField] public int[] RankEssenceCosts { get; private set; } = new int[0];

        [field: Header("Prerequisites")]
        [Tooltip("Other upgrades that must already be acquired this run before this one can be offered. " +
                 "Checked against RunState.appliedUpgrades — applies to drafts and the in-run shop.")]
        [field: SerializeField] public UpgradeDefinition[] Prerequisites { get; private set; } = new UpgradeDefinition[0];

        /// <summary>True if this upgrade can be acquired more than once.</summary>
        public bool IsStackable => MaxRank > 1;

        /// <summary>
        /// Applies this upgrade's effect to RuntimeStats. `count` is the number of
        /// acquired ranks; every stat scales linearly (total = ModifierValue × count).
        /// </summary>
        public void Apply(RuntimeStats stats, int count = 1)
        {
            if (stats == null || count <= 0) return;

            float total = ModifierValue * count;

            switch (Stat)
            {
                case UpgradeStat.YuumiRotationSpeed:
                    stats.YuumiRotationSpeed += total;
                    break;
                case UpgradeStat.ChargePerBall:
                    stats.ChargePerBallDestroyed += Mathf.RoundToInt(total);
                    break;
                case UpgradeStat.PierceWidth:
                    stats.PierceWidthMultiplier += total;
                    break;
                // BombRadius is deprecated — explosion radius is count-scaled from red
                // synergy upgrades (RunManager.RecomputeSynergyStats), not an upgrade card.
                case UpgradeStat.GoldGainBonus:
                    stats.GoldGainMultiplier += total;
                    break;
                case UpgradeStat.GoldPerCascade:
                    stats.GoldPerCascade += Mathf.RoundToInt(total);
                    break;
                case UpgradeStat.ShopReroll:
                    stats.ShopRerollEnabled = true;
                    break;
                case UpgradeStat.EssenceGain:
                    stats.EssenceGainMultiplier += total;
                    break;
                case UpgradeStat.BallSpeedReduction:
                    stats.BallSpeedReduction += total;
                    break;
                case UpgradeStat.DraftReroll:
                    stats.DraftRerollCount += Mathf.RoundToInt(total);
                    break;
                case UpgradeStat.StartingGold:
                    stats.StartingGold += Mathf.RoundToInt(total);
                    break;
                // ColorWeight is deprecated — colour spawn weight is count-scaled from
                // synergy upgrades (RunManager.RecomputeSynergyStats), not an upgrade card.
                case UpgradeStat.ColorMatchGold:
                    stats.AddColorMatchGold(TargetColor, Mathf.RoundToInt(total));
                    break;
                // HomingStrict / HomingLoose are deprecated — rage meter (purple synergy)
                // sets these flags dynamically while rage is active. Don't author cards.
                case UpgradeStat.HomingRange:
                    stats.HomingRange += total;
                    break;
                case UpgradeStat.RedMatchExplosion:
                    stats.RedMatchExplosionEnabled = true;
                    break;
                case UpgradeStat.ExplosionThresholdReduction:
                    stats.ExplosionThresholdReduction += Mathf.RoundToInt(total);
                    break;
                case UpgradeStat.BombSpawnWeight:
                    stats.BombAwardWeight += total;
                    break;
                case UpgradeStat.RageBuildupRate:
                    stats.RageBuildupBonus += total;
                    break;
                case UpgradeStat.RageDuration:
                    stats.RageDurationBonus += total;
                    break;
                case UpgradeStat.FireRate:
                    stats.FireRateBonus += total;
                    break;
                case UpgradeStat.RageUnlock:
                    stats.RageEnabled = true;
                    break;
            }
        }

        /// <summary>
        /// Returns true if every prerequisite upgrade has already been acquired in this
        /// run (i.e. is present in RunState.appliedUpgrades). Empty prerequisites = always met.
        /// </summary>
        public bool ArePrerequisitesMet(RunState state)
        {
            if (Prerequisites == null || Prerequisites.Length == 0) return true;
            if (state == null) return false;
            for (int i = 0; i < Prerequisites.Length; i++)
            {
                if (Prerequisites[i] != null && !state.HasUpgrade(Prerequisites[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Essence cost of the purchase at the given 0-based purchase index
        /// (0 = first purchase). Clamps to the array bounds.
        /// </summary>
        public int GetEssenceCost(int purchaseIndex)
        {
            if (RankEssenceCosts == null || RankEssenceCosts.Length == 0) return 0;
            int i = Mathf.Clamp(purchaseIndex, 0, RankEssenceCosts.Length - 1);
            return RankEssenceCosts[i];
        }

        /// <summary>
        /// Human-readable summary of the cumulative effect after `count` ranks.
        /// Used by shop UI to show the current/next bonus.
        /// </summary>
        public string GetEffectSummary(int count)
        {
            if (count <= 0) return "—";

            float total = ModifierValue * count;

            switch (Stat)
            {
                // Multiplier stats — show the accumulated bonus as a percentage.
                case UpgradeStat.PierceWidth:
                case UpgradeStat.GoldGainBonus:
                case UpgradeStat.EssenceGain:
                    return $"+{total * 100f:F0}%";
                case UpgradeStat.BallSpeedReduction:
                    return $"-{total * 100f:F0}% speed";
                // Raw integer stats.
                case UpgradeStat.ChargePerBall:
                case UpgradeStat.GoldPerCascade:
                case UpgradeStat.DraftReroll:
                case UpgradeStat.StartingGold:
                    return $"+{Mathf.RoundToInt(total)}";
                case UpgradeStat.ColorMatchGold:
                    return $"+{Mathf.RoundToInt(total)} gold / {TargetColor} match";
                case UpgradeStat.ExplosionThresholdReduction:
                    return $"-{Mathf.RoundToInt(total)} explosion threshold";
                // Raw float stats.
                case UpgradeStat.YuumiRotationSpeed:
                    return $"+{total:F1}";
                case UpgradeStat.BombSpawnWeight:
                    return $"+{total:F1} bomb weight";
                case UpgradeStat.RageBuildupRate:
                    return $"+{total:F1} rage per ball";
                case UpgradeStat.RageDuration:
                    return $"+{total:F1}s rage duration";
                case UpgradeStat.FireRate:
                    return $"-{total:F2}s fire cooldown";
                case UpgradeStat.HomingRange:
                    return $"+{total:F1} homing range";
                case UpgradeStat.ShopReroll:
                case UpgradeStat.HomingStrict:
                case UpgradeStat.HomingLoose:
                case UpgradeStat.RedMatchExplosion:
                case UpgradeStat.RageUnlock:
                    return "Enabled";
                default:
                    return "—";
            }
        }
    }
}
