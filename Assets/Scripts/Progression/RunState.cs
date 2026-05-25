using System.Collections.Generic;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// In-memory state for a single run. Owned by RunManager.
    /// Discarded at run end — meta progression lives in PlayerProfile.
    ///
    /// Upgrade list and currency are stubs for now; they're populated starting at step 4.
    /// </summary>
    public class RunState
    {
        public RunNode[] nodes;
        public int currentNodeIndex;
        public int gold;
        public List<UpgradeDefinition> appliedUpgrades = new List<UpgradeDefinition>();

        public RunNode CurrentNode =>
            (nodes != null && currentNodeIndex >= 0 && currentNodeIndex < nodes.Length)
                ? nodes[currentNodeIndex]
                : null;

        public bool IsLastNode => nodes != null && currentNodeIndex >= nodes.Length - 1;
        public int FloorsCleared => currentNodeIndex; // how many nodes the player walked past

        /// <summary>
        /// Returns true if the player has already acquired this upgrade in this run.
        /// </summary>
        public bool HasUpgrade(UpgradeDefinition upgrade)
        {
            return upgrade != null && appliedUpgrades.Contains(upgrade);
        }

        /// <summary>
        /// How many times this upgrade has been acquired in this run.
        /// </summary>
        public int CountUpgrade(UpgradeDefinition upgrade)
        {
            if (upgrade == null) return 0;
            int count = 0;
            for (int i = 0; i < appliedUpgrades.Count; i++)
                if (appliedUpgrades[i] == upgrade) count++;
            return count;
        }

        /// <summary>
        /// How many colour-synergy upgrades of the given colour have been acquired this run.
        /// Drives count-based synergy scaling (e.g. explosion radius growing per red upgrade).
        /// </summary>
        public int CountColorSynergyUpgrades(YuumisProwl.BallColor color)
        {
            int count = 0;
            for (int i = 0; i < appliedUpgrades.Count; i++)
            {
                var u = appliedUpgrades[i];
                if (u != null && u.IsColorSynergy && u.TargetColor == color)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// True if this upgrade can still be offered/acquired this run — i.e. the player
        /// hasn't already hit its MaxRank. Used to filter drafts and shop offerings.
        /// </summary>
        public bool CanAcquire(UpgradeDefinition upgrade)
        {
            return upgrade != null && CountUpgrade(upgrade) < upgrade.MaxRank;
        }
    }
}
