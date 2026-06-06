using UnityEngine;

namespace YuumisProwl
{
    [CreateAssetMenu(fileName = "BossData_", menuName = "Yuumi/Boss Data")]

    public class BossData : ScriptableObject
    {

        public string BossName;
        [Header("Boss Settings")]
        public int maxHealth;

        [Header("Wave Settings")]
        public float waveSpawnInterval;
        public float ballSpeedMultiplier;
        public int waveDamage;
        
        [Header("Enemy Settings")]
        [Tooltip("In-chain disruptor enemies injected into each wave. One entry per enemy type.")]
        public EnemySpawn[] enemySpec;

        [Tooltip("Fraction of ball-clear damage the boss ignores while any Warden is alive in the chain (0 = no shield, 1 = fully immune).")]
        [Range(0f, 1f)] public float wardenDamageReduction = 0.5f;

        /// <summary>
        /// Bonus boss damage dealt when an enemy of the given type is cleared, from the spec
        /// (0 if the type isn't authored). Incentivises hunting enemies.
        /// </summary>
        public int GetClearBonus(EnemyType type)
        {
            if (enemySpec == null) return 0;
            for (int i = 0; i < enemySpec.Length; i++)
                if (enemySpec[i] != null && enemySpec[i].type == type)
                    return enemySpec[i].clearBonus;
            return 0;
        }
    }

    /// <summary>
    /// One enemy type's per-wave authoring: how many spawn each wave and the bonus boss
    /// damage clearing one deals.
    /// </summary>
    [System.Serializable]
    public class EnemySpawn
    {
        public EnemyType type = EnemyType.Stone;
        [Tooltip("How many of this enemy are injected into each wave.")]
        [Min(0)] public int perWave = 1;
        [Tooltip("Bonus boss damage dealt when one of these is cleared (on top of any colour-match damage).")]
        [Min(0)] public int clearBonus = 5;
    }
}