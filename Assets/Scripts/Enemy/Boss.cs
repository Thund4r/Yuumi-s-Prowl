using UnityEngine;

namespace YuumisProwl.Enemy
{
    public class Boss : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BossData bossData;
        private int Health;
        public System.Action OnDefeated;

        private void Start()
        {
            Health = bossData.maxHealth;
            
        }

        public void TakeDamage(int damage)
        {
            Health = Mathf.Max(0, Health - damage);

            if (Health <= 0)
            {
                OnDefeated?.Invoke();
            }
        }

    }
}