using YuumisProwl.Managers;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// What kind of stop a node represents on a run.
    /// More types (Shop, Elite, Event, Boss) will be added as the system grows.
    /// </summary>
    public enum RunNodeType
    {
        Gameplay,
        Shop
    }

    /// <summary>
    /// One step in a run. For Gameplay nodes, mapPrefab is the Map to load.
    /// For Shop (and future) nodes, mapPrefab is unused.
    ///
    /// Plain serializable class so RunState can survive being inspected/logged easily.
    /// </summary>
    [System.Serializable]
    public class RunNode
    {
        public RunNodeType type;
        public Map mapPrefab;

        public RunNode(RunNodeType type, Map mapPrefab = null)
        {
            this.type = type;
            this.mapPrefab = mapPrefab;
        }
    }
}
