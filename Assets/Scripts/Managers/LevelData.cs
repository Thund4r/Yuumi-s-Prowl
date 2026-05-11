using UnityEngine;

namespace YuumisProwl
{
    /// <summary>
    /// Per-map tuning data. Assigned on a Map prefab's root component and applied
    /// by LevelManager when the map is loaded. Create assets via:
    /// Right-click in Project → Yuumi → Level Data
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Yuumi/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Display")]
        public string levelName = "Level 1";

        [Header("Ball Settings")]
        [Tooltip("Total number of balls to spawn across the whole level.")]
        public int totalBalls = 50;
        [Tooltip("How many different colors are active for this level (1–6).")]
        [Range(1, 6)]
        public int colorCount = 4;

        [Header("Difficulty")]
        [Tooltip("Speed at which the ball chain moves along the path.")]
        public float ballSpeed = 2f;
    }
}
