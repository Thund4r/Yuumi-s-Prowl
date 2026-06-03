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
        
        [Header("Enemy Settings")]
        public float enemySpawnInterval;
        
    }
}