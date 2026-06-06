using System.Collections.Generic;
using UnityEngine;
using YuumisProwl;
using YuumisProwl.BallChain;
using YuumisProwl.Enemy;
using YuumisProwl.VFX;

namespace YuumisProwl.Managers
{
    public class BossManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private Boss bossPrefab;

        [Header("Damage Bolts")]
        [Tooltip("If true, ball-clear and enemy-clear damage flies to the boss as a HomingBolt and only lands on arrival. If false, damage is applied instantly.")]
        [SerializeField] private bool useDamageBolts = true;
        [Tooltip("Prefab for the boss-damage bolt (must have a BossDamageBolt component). If null, a red placeholder is created at runtime.")]
        [SerializeField] private BossDamageBolt damageBoltPrefab;
        [Tooltip("Fallback origin for damage that has no destruction position (e.g. AoE removals). If null, that damage is applied instantly with no bolt.")]
        [SerializeField] private Transform fallbackDamageOrigin;
        [SerializeField, Min(0.1f)] private float damageBoltSpeed = 14f;
        [SerializeField, Min(0.05f)] private float damageBoltArrivalDistance = 0.5f;
        [SerializeField, Min(1)] private int initialBoltPoolSize = 16;

        private Boss currentBoss;
        public System.Action OnBossDefeated;

        /// <summary>The live boss's authoring data (enemy spec, warden shield) — null between floors.</summary>
        public BossData CurrentBossData => currentBoss != null ? currentBoss.Data : null;

        // Reused buffer for the warden scan so it doesn't allocate per match.
        private readonly List<BallNode> chainBuffer = new List<BallNode>();

        // Damage-bolt pool. Bolts are parented to this (persistent) manager so a map teardown
        // doesn't destroy a bolt mid-flight.
        private readonly Queue<BossDamageBolt> boltPool = new Queue<BossDamageBolt>(16);
        private readonly List<BossDamageBolt> activeBolts = new List<BossDamageBolt>(16);

        // Per-ball positions + damages captured from OnMatchBallsDamaged so each cleared ball fires
        // its own bolt from where it died, carrying its own damage. That event fires immediately
        // before the matching OnBallsDestroyed, so the flag is reliably fresh for match damage and
        // stale (→ fallback) for AoE removals, which fire OnBallsDestroyed with no preceding event.
        private readonly List<Vector3> lastMatchPositions = new List<Vector3>(8);
        private readonly List<int> lastMatchDamages = new List<int>(8);
        private bool hasFreshPositions;

        // Cached delegates so per-ball bolt launches don't allocate a delegate each time.
        private System.Action<BossDamageBolt, float> boltArrivedCb;
        private System.Action<BossDamageBolt> boltLostCb;

        private void Start()
        {
            if (bossPrefab == null)
            {
                Debug.LogError("BossManager: Boss prefab not assigned!");
                return;
            }
            if (matchProcessor != null)
            {
                matchProcessor.OnBallsDestroyed += HandleBallsDestroyed;
                matchProcessor.OnMatchBallsDamaged += HandleMatchBallsDamaged;
            }
            if (ballChainManager != null)
            {
                ballChainManager.OnEnemyDestroyed += HandleEnemyDestroyed;
            }

            boltArrivedCb = BoltArrived;
            boltLostCb = BoltLost;

            for (int i = 0; i < initialBoltPoolSize; i++)
                boltPool.Enqueue(CreateBolt());
        }

        private void OnDestroy()
        {
            if (matchProcessor != null)
            {
                matchProcessor.OnBallsDestroyed -= HandleBallsDestroyed;
                matchProcessor.OnMatchBallsDamaged -= HandleMatchBallsDamaged;
            }
            if (ballChainManager != null)
            {
                ballChainManager.OnEnemyDestroyed -= HandleEnemyDestroyed;
            }
        }

        public void SpawnBoss(Transform bossSpawnPoint, float healthMult = 1f)
        {
            if (bossSpawnPoint == null)
            {
                Debug.LogError("BossManager: Boss spawn point is null!");
                return;
            }
            // Any bolts still chasing the previous floor's boss are stale — recycle them.
            ReleaseAllBolts();
            hasFreshPositions = false;

            currentBoss = Instantiate(bossPrefab, bossSpawnPoint.position, bossSpawnPoint.rotation, bossSpawnPoint);
            currentBoss.SetHealthMultiplier(healthMult);
            currentBoss.OnDefeated += HandleBossDefeated;
        }

        private void HandleMatchBallsDamaged(List<Vector3> positions, List<int> damages)
        {
            if (positions == null || positions.Count == 0) return;
            lastMatchPositions.Clear();
            lastMatchPositions.AddRange(positions);
            lastMatchDamages.Clear();
            if (damages != null) lastMatchDamages.AddRange(damages);
            hasFreshPositions = true;
        }

        public void HandleBallsDestroyed(int count, BallColor color)
        {
            if (currentBoss == null) return;

            bool haveBallData = hasFreshPositions
                && lastMatchPositions.Count == count
                && lastMatchDamages.Count == count;
            hasFreshPositions = false;

            // Warden shield (evaluated post-removal, so the hit that kills the last Warden is full).
            // Applied as a per-ball float factor — no integer rounding, so equal-damage balls stay
            // equal-sized and there's no leftover-distribution artifact.
            float shieldFactor = 1f;
            BossData data = currentBoss.Data;
            if (data != null && data.wardenDamageReduction > 0f && AnyWardenAlive())
                shieldFactor = 1f - data.wardenDamageReduction;

            if (shieldFactor <= 0f) return; // fully shielded — no damage, no bolts

            if (useDamageBolts && haveBallData)
            {
                // One bolt per cleared ball, each carrying its own damageValue × shield.
                for (int i = 0; i < lastMatchPositions.Count; i++)
                {
                    float dmg = lastMatchDamages[i] * shieldFactor;
                    if (dmg > 0f) SpawnBolt(lastMatchPositions[i], dmg);
                }
            }
            else
            {
                // AoE / positionless removal (no per-ball data): a single bolt from the fallback.
                DealBossDamage(count * shieldFactor, null);
            }
        }

        /// <summary>
        /// An enemy disruptor was cleared — deal its per-type clear-bonus to the boss, on top of
        /// any colour-match damage that already landed for it.
        /// </summary>
        private void HandleEnemyDestroyed(EnemyType type, Vector3 worldPos)
        {
            if (currentBoss == null || currentBoss.Data == null) return;
            int bonus = currentBoss.Data.GetClearBonus(type);
            DealBossDamage(bonus, worldPos);
        }

        /// <summary>
        /// Applies `amount` damage to the boss. When damage bolts are enabled and an origin is
        /// available, the damage flies there as a HomingBolt and only lands on arrival (the boss is
        /// defeated event-driven via Boss.OnDefeated when the lethal bolt connects). Otherwise it's
        /// applied instantly.
        /// </summary>
        private void DealBossDamage(float amount, Vector3? origin)
        {
            if (currentBoss == null || amount <= 0f) return;

            if (!useDamageBolts)
            {
                currentBoss.TakeDamage(amount);
                return;
            }

            Vector3? launchFrom = origin
                ?? (fallbackDamageOrigin != null ? fallbackDamageOrigin.position : (Vector3?)null);

            if (!launchFrom.HasValue)
            {
                // No position to launch from — apply instantly rather than drop the damage.
                currentBoss.TakeDamage(amount);
                return;
            }

            SpawnBolt(launchFrom.Value, amount);
        }

        private void SpawnBolt(Vector3 origin, float dmg)
        {
            if (currentBoss == null) return;
            BossDamageBolt bolt = AcquireBolt();
            activeBolts.Add(bolt);
            bolt.Launch(origin, currentBoss.transform, dmg,
                damageBoltSpeed, damageBoltArrivalDistance, boltArrivedCb, boltLostCb);
        }

        private void BoltArrived(BossDamageBolt bolt, float dmg)
        {
            // The boss may already be gone (an earlier bolt was lethal) — then drop the damage.
            if (currentBoss != null)
                currentBoss.TakeDamage(dmg);
            ReleaseBolt(bolt);
        }

        private void BoltLost(BossDamageBolt bolt)
        {
            ReleaseBolt(bolt);
        }

        /// <summary>True if any Warden is currently in the chain (scans the live ball list).</summary>
        private bool AnyWardenAlive()
        {
            if (ballChainManager == null) return false;
            ballChainManager.GetBallChainNonAlloc(chainBuffer);
            for (int i = 0; i < chainBuffer.Count; i++)
                if (chainBuffer[i].enemyType == EnemyType.Warden) return true;
            return false;
        }

        /// <summary>
        /// Deals the per-wave-clear damage chunk to the boss. Like ball-clear damage it now flies in
        /// as a bolt and only lands on arrival; because the result is no longer synchronous, the
        /// caller passes onBossSurvived, which is invoked once the chunk lands and the boss is still
        /// alive (so GameManager can spawn the next wave then). If the chunk is lethal, the win goes
        /// through OnBossDefeated instead and onBossSurvived is never called.
        /// </summary>
        public void HandleWaveCleared(System.Action onBossSurvived)
        {
            if (currentBoss == null) return; // no boss → no next wave (matches the old "treat as defeated")

            int waveDamage = currentBoss.Data != null ? currentBoss.Data.waveDamage : 0;
            if (waveDamage <= 0)
            {
                onBossSurvived?.Invoke();
                return;
            }

            if (!useDamageBolts)
            {
                bool defeated = currentBoss.TakeDamage(waveDamage);
                if (!defeated) onBossSurvived?.Invoke();
                return;
            }

            // The wave chunk has no ball position — launch it from the fallback origin, or from
            // just below the boss so it visibly rises into it when no fallback is set.
            Vector3 origin = fallbackDamageOrigin != null
                ? fallbackDamageOrigin.position
                : currentBoss.transform.position + Vector3.down * 4f;

            BossDamageBolt bolt = AcquireBolt();
            activeBolts.Add(bolt);
            bolt.Launch(origin, currentBoss.transform, waveDamage,
                damageBoltSpeed, damageBoltArrivalDistance,
                (b, dmg) => WaveBoltArrived(b, dmg, onBossSurvived), boltLostCb);
        }

        private void WaveBoltArrived(BossDamageBolt bolt, float dmg, System.Action onBossSurvived)
        {
            bool defeated = false;
            if (currentBoss != null) defeated = currentBoss.TakeDamage(dmg);
            ReleaseBolt(bolt);
            if (!defeated && currentBoss != null) onBossSurvived?.Invoke();
        }

        private void HandleBossDefeated()
        {
            OnBossDefeated?.Invoke();
            currentBoss.OnDefeated -= HandleBossDefeated;
            Destroy(currentBoss.gameObject);
            currentBoss = null;
            ReleaseAllBolts();
        }

        // --------------------------------------------------------------
        // Damage-bolt pool
        // --------------------------------------------------------------

        private BossDamageBolt AcquireBolt()
        {
            return boltPool.Count > 0 ? boltPool.Dequeue() : CreateBolt();
        }

        private BossDamageBolt CreateBolt()
        {
            BossDamageBolt bolt;
            if (damageBoltPrefab != null)
            {
                bolt = Instantiate(damageBoltPrefab, transform);
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform);
                go.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(1f, 0.15f, 0.1f);
                bolt = go.AddComponent<BossDamageBolt>();
                go.name = "BossDamageBolt_Placeholder";
            }
            bolt.gameObject.SetActive(false);
            return bolt;
        }

        private void ReleaseBolt(BossDamageBolt bolt)
        {
            if (bolt == null) return;
            activeBolts.Remove(bolt);
            bolt.HardReset();
            boltPool.Enqueue(bolt);
        }

        private void ReleaseAllBolts()
        {
            for (int i = activeBolts.Count - 1; i >= 0; i--)
            {
                var bolt = activeBolts[i];
                if (bolt != null)
                {
                    bolt.HardReset();
                    boltPool.Enqueue(bolt);
                }
            }
            activeBolts.Clear();
        }
    }
}
