using UnityEngine;
using UnityEngine.UI;

namespace YuumisProwl.Enemy
{
    public class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private Boss boss;          // sibling on the prefab
        [SerializeField] private Image healthBar;         // the Filled image
        [SerializeField] private Image chaseHealthBar;
        [SerializeField] private float delayBeforeChase = 3f;
        private float pastHealth = 1f;
        private float targetHealth = 1f;
        private float delayTimer = 0f;
        private Coroutine healthChaseRoutine;
        void OnEnable()  { boss.OnHealthChanged += Refresh; }
        void OnDisable() { boss.OnHealthChanged -= Refresh; }
        void Refresh(float cur, float max) 
        { 
            healthBar.fillAmount = max > 0f ? cur / max : 0f;
            targetHealth = healthBar.fillAmount;
            delayTimer = delayBeforeChase;
        }

        void Update()
        {
            if (delayTimer > 0f)
            {
                delayTimer -= Time.deltaTime;
            }
            else if (pastHealth > targetHealth)
            {
                pastHealth = Mathf.MoveTowards(pastHealth, targetHealth, Time.deltaTime / 0.5f);
                chaseHealthBar.fillAmount = pastHealth;
            }
        }
    }

}