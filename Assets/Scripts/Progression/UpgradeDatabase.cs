using System.Collections.Generic;
using UnityEngine;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Master list of every UpgradeDefinition in the game. A single shared asset that
    /// both RunManager and MetaShopUI reference — register an upgrade here once instead
    /// of dragging it into multiple scene objects across scenes.
    ///
    /// Create via: Right-click in Project → Yuumi → Upgrade Database
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Yuumi/Upgrade Database")]
    public class UpgradeDatabase : ScriptableObject
    {
        [Tooltip("Every upgrade in the game. The availability flags on each upgrade " +
                 "(IsDraftable / IsShoppable / IsMetaShop) decide where it actually appears.")]
        [SerializeField] private UpgradeDefinition[] allUpgrades = new UpgradeDefinition[0];

        public UpgradeDefinition[] AllUpgrades => allUpgrades;

        /// <summary>Finds an upgrade by its stable UpgradeId, or null if not present.</summary>
        public UpgradeDefinition FindById(string upgradeId)
        {
            if (allUpgrades == null || string.IsNullOrEmpty(upgradeId)) return null;
            for (int i = 0; i < allUpgrades.Length; i++)
            {
                if (allUpgrades[i] != null && allUpgrades[i].UpgradeId == upgradeId)
                    return allUpgrades[i];
            }
            return null;
        }

        /// <summary>Returns every upgrade flagged IsMetaShop.</summary>
        public List<UpgradeDefinition> GetMetaShopUpgrades()
        {
            var result = new List<UpgradeDefinition>();
            if (allUpgrades == null) return result;
            for (int i = 0; i < allUpgrades.Length; i++)
            {
                if (allUpgrades[i] != null && allUpgrades[i].IsMetaShop)
                    result.Add(allUpgrades[i]);
            }
            return result;
        }
    }
}
