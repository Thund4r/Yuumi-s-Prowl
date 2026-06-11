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

        [Header("Charge System")]
        [Tooltip("Charge earned per ball destroyed in a match.")]
        public int chargePerBallDestroyed = 1;
        [Tooltip("Bonus charge per cascade in a match sequence.")]
        public int cascadeBonusCharge = 3;
        [Tooltip("Charge required to earn a random power-up.")]
        public int chargeThreshold = 10;
        [Tooltip("Maximum number of potions the player can hold at once.")]
        public int maxPowerUpSlots = 4;

        [Header("Pierce")]
        [Tooltip("Maximum world-space distance a Pierce projectile travels before despawning.")]
        public float pierceMaxDistance = 30f;
        [Tooltip("Travel speed multiplier for Pierce projectiles (relative to normal homing speed).")]
        public float pierceSpeedMultiplier = 2f;

        [Header("Bomb")]
        [Tooltip("World-space radius of the explosion that destroys balls on contact.")]
        public float bombRadius = 3f;

        [Header("Freeze")]
        [Tooltip("Seconds the chain stops advancing when a Freeze potion is used.")]
        public float freezeDuration = 4f;
    }
}
