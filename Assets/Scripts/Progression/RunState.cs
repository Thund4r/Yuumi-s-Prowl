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
        public List<object> appliedUpgrades = new List<object>(); // typed in step 4 (UpgradeDefinition)

        public RunNode CurrentNode =>
            (nodes != null && currentNodeIndex >= 0 && currentNodeIndex < nodes.Length)
                ? nodes[currentNodeIndex]
                : null;

        public bool IsLastNode => nodes != null && currentNodeIndex >= nodes.Length - 1;
        public int FloorsCleared => currentNodeIndex; // how many nodes the player walked past
    }
}
