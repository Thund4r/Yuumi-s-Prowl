using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Placed on the root of a map prefab. Exposes the per-map references
    /// (PathController, LevelData) that LevelManager binds into the persistent
    /// scene systems after the prefab is instantiated.
    /// </summary>
    public class Map : MonoBehaviour
    {
        [SerializeField] private PathController pathController;
        [SerializeField] private LevelData levelData;

        public PathController PathController => pathController;
        public LevelData LevelData => levelData;
    }
}
