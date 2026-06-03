using UnityEngine;

namespace YuumisProwl.Enemy
{
    public class Boss : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BossData bossData;
        private int Health;
        private float HealthMultiplier = 1;
        public System.Action OnDefeated;

        private void Start()
        {
            Health = Mathf.RoundToInt(bossData.maxHealth * HealthMultiplier);
        }

        public bool TakeDamage(int damage)
        {
            Health = Mathf.Max(0, Health - damage);
            if (Health <= 0)
            {
                OnDefeated?.Invoke();
                return true;
            }
            return false;
        }

        public bool TakeWaveDamage()
        {
            return TakeDamage(bossData.waveDamage);
        }

        public void SetHealthMultiplier(float multiplier)
        {
            HealthMultiplier = multiplier;
        }

    }
}