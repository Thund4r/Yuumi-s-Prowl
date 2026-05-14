using System.Collections.Generic;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Persistent player progression data (meta upgrades, essence, unlocks, etc.).
    /// Serializable to JSON for save/load. Owned by PlayerProfileManager.
    /// </summary>
    [System.Serializable]
    public class PlayerProfile
    {
        public int essenceTotal;
        public int essenceSpent;
        public MetaUpgradeState[] metaUpgrades = new MetaUpgradeState[0];

        public PlayerProfile()
        {
            essenceTotal = 0;
            essenceSpent = 0;
        }
    }

    /// <summary>
    /// State of a single meta upgrade (current rank/level).
    /// Rank is 0-based: rank 0 = 1st purchase, rank 1 = 2nd purchase, etc.
    /// </summary>
    [System.Serializable]
    public class MetaUpgradeState
    {
        public string upgradeId; // e.g. "ChargePerBall", "EssenceGain"
        public int rank; // 0-based rank (0 = level 1, 1 = level 2, etc.)

        public MetaUpgradeState(string id)
        {
            upgradeId = id;
            rank = -1; // -1 = not purchased yet
        }
    }
}
