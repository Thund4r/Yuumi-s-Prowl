using UnityEngine;

namespace YuumisProwl
{
    /// <summary>
    /// Defines the parameters for a single level.
    /// Create assets via: Right-click in Project → Yuumi → Level Data
    /// The path shape is defined by the SplineContainer in the scene;
    /// different maps require different scenes.
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

        [Header("Scene Transition")]
        [Tooltip("Exact name of the scene to load when this level is won. Leave empty for no transition (final level).")]
        public string nextSceneName = "";
        [Tooltip("Exact name of the scene to reload when this level is lost. Leave empty to reload the current scene.")]
        public string retrySceneName = "";
    }
}
