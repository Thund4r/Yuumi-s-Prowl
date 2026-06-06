using UnityEngine;

namespace YuumisProwl.Enemy
{
    public class Boss : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BossData bossData;
        public BossData Data => bossData;
        private float Health;
        private float MaxHealth;
        private float HealthMultiplier = 1f;
        private bool defeated;
        public System.Action OnDefeated;
        public System.Action<float, float> OnHealthChanged; // (current, max)

        private void Start()
        {
            MaxHealth = bossData.maxHealth * HealthMultiplier;
            Health = MaxHealth;
        }

        public bool TakeDamage(float damage)
        {
            if (defeated) return true;
            Health = Mathf.Max(0f, Health - damage);
            OnHealthChanged?.Invoke(Health, MaxHealth);
            if (Health <= 0f)
            {
                defeated = true;
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