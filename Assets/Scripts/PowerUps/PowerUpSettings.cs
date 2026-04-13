using UnityEngine;

namespace YuumisProwl.PowerUps
{
    /// <summary>
    /// Shared settings asset for the power-up system.
    /// Create one via: Right-click in Project → Yuumi → Power-Up Settings
    /// Assign it to every scene's PowerUpSpawner so tuning stays consistent across levels.
    /// </summary>
    [CreateAssetMenu(fileName = "PowerUpSettings", menuName = "Yuumi/Power-Up Settings")]
    public class PowerUpSettings : ScriptableObject
    {
        [Header("Hammer")]
        [Tooltip("How far (world units) the chain is pushed back when a Hammer ball is hit.")]
        public float hammerRecoilDistance = 3f;

        [Header("Natural Spawn")]
        [Tooltip("Seconds between natural spawn roll attempts.")]
        public float naturalSpawnInterval = 20f;
        [Tooltip("Probability (0–1) that a Hammer spawns on each roll.")]
        [Range(0f, 1f)]
        public float naturalSpawnChance = 0.4f;

        [Header("Reward Spawn")]
        [Tooltip("Minimum number of cascade matches in a single sequence to reward a Hammer spawn.")]
        public int rewardCascadeThreshold = 2;
    }
}
