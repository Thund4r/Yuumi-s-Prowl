using UnityEngine;
using YuumisProwl.BallChain;
using YuumisProwl.Enemy;

namespace YuumisProwl.Managers
{
    public class BossManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private Boss bossPrefab;
        private Boss currentBoss;
        public System.Action OnBossDefeated;

        private void Start()
        {
            if (bossPrefab == null)
            {
                Debug.LogError("BossManager: Boss prefab not assigned!");
                return;
            }
            matchProcessor.OnBallsDestroyed += HandleBallsDestroyed;
        }

        public void SpawnBoss(Transform bossSpawnPoint)
        {
            currentBoss = Instantiate(bossPrefab, bossSpawnPoint.position, bossSpawnPoint.rotation);
            currentBoss.OnDefeated += HandleBossDefeated;
        }

        public void HandleBallsDestroyed(int count, BallColor color)
        {
            currentBoss.TakeDamage(count);
        }

        private void HandleBossDefeated()
        {
            OnBossDefeated?.Invoke();
            currentBoss.OnDefeated -= HandleBossDefeated;
            Destroy(currentBoss.gameObject);
        }

    }
}