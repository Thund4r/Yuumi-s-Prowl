using UnityEngine;
using UnityEngine.UI;

namespace YuumisProwl.Enemy
{
    public class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private Boss boss;          // sibling on the prefab
        [SerializeField] private Image healthBar;         // the Filled image
        void OnEnable()  { boss.OnHealthChanged += Refresh; }
        void OnDisable() { boss.OnHealthChanged -= Refresh; }
        void Refresh(float cur, float max) { healthBar.fillAmount = max > 0f ? cur / max : 0f; }
    }

}