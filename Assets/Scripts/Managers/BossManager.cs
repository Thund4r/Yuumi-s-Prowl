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

        public void SpawnBoss(Transform bossSpawnPoint, float healthMult = 1f)
        {
            if (bossSpawnPoint == null)
            {
                Debug.LogError("BossManager: Boss spawn point is null!");
                return;
            }
            currentBoss = Instantiate(bossPrefab, bossSpawnPoint.position, bossSpawnPoint.rotation, bossSpawnPoint);
            currentBoss.SetHealthMultiplier(healthMult);
            currentBoss.OnDefeated += HandleBossDefeated;
        }

        public void HandleBallsDestroyed(int count, BallColor color)
        {
            currentBoss.TakeDamage(count);
        }

        public void HandleWaveCleared()
        {
            currentBoss.TakeWaveDamage();
        }

        private void HandleBossDefeated()
        {
            OnBossDefeated?.Invoke();
            currentBoss.OnDefeated -= HandleBossDefeated;
            Destroy(currentBoss.gameObject);
        }

    }
}