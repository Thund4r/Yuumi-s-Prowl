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
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private Boss bossPrefab;
        

        [Header("Damage Bolts")]
        [Tooltip("If true, ball-clear and enemy-clear damage flies to the boss as a HomingBolt and only lands on arrival. If false, damage is applied instantly.")]
        [SerializeField] private bool useDamageBolts = true;
        [Tooltip("Prefab for the boss-damage bolt (must have a BossDamageBolt component). If null, a red placeholder is created at runtime.")]
        [SerializeField] private BossDamageBolt damageBoltPrefab;
        [Tooltip("Origin for the per-wave-clear damage chunk (the only damage with no ball position). If null, the wave chunk launches from just below the boss.")]
        [SerializeField] private Vector3 fallbackDamageOrigin;
        [SerializeField, Min(0.1f)] private float damageBoltSpeed = 14f;
        [SerializeField, Min(0.05f)] private float damageBoltArrivalDistance = 0.5f;
        [SerializeField, Min(1)] private int initialBoltPoolSize = 16;

        private Boss currentBoss;
        private Coroutine damageFlashRoutine;
        public System.Action OnBossDefeated;

        /// <summary>The live boss's authoring data (enemy spec, warden shield) — null between floors.</summary>
        public BossData CurrentBossData => currentBoss != null ? currentBoss.Data : null;

        // Reused buffer for the warden scan so it doesn't allocate per match.
        private readonly List<BallNode> chainBuffer = new List<BallNode>();

        // Damage-bolt pool. Bolts are parented to this (persistent) manager so a map teardown
        // doesn't destroy a bolt mid-flight.
        private readonly Queue<BossDamageBolt> boltPool = new Queue<BossDamageBolt>(16);
        private readonly List<BossDamageBolt> activeBolts = new List<BossDamageBolt>(16);

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
            // Single hook: every destroyed ball (match, pierce, bomb, explosion, icicle, hammer)
            // arrives here via BallChainManager's unified destruction event.
            if (ballChainManager != null)
            {
                ballChainManager.OnBallsDestroyed += HandleBallsDestroyed;
            }

            boltArrivedCb = BoltArrived;
            boltLostCb = BoltLost;

            for (int i = 0; i < initialBoltPoolSize; i++)
                boltPool.Enqueue(CreateBolt());
        }

        private void OnDestroy()
        {
            if (ballChainManager != null)
            {
                ballChainManager.OnBallsDestroyed -= HandleBallsDestroyed;
            }
        }


        public void setFallbackDamageOrigin(Vector3 origin)
        {
            fallbackDamageOrigin = origin;
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

            currentBoss = Instantiate(bossPrefab, bossSpawnPoint.position, bossSpawnPoint.rotation, bossSpawnPoint);
            currentBoss.SetHealthMultiplier(healthMult);
            currentBoss.OnDefeated += HandleBossDefeated;
        }

        /// <summary>
        /// Every ball that leaves the chain — by any means — lands here. Each fires one damage bolt
        /// from its own position carrying its own damageValue (× the Warden shield) plus, for enemy
        /// balls, the per-type clear-bonus. The shield is evaluated post-removal, so the hit that
        /// kills the last Warden lands at full.
        /// </summary>
        private void HandleBallsDestroyed(List<BallDestructionInfo> balls)
        {
            if (currentBoss == null || balls == null || balls.Count == 0) return;

            BossData data = currentBoss.Data;
            float shieldFactor = 1f;
            if (data != null && data.wardenDamageReduction > 0f && AnyWardenAlive())
                shieldFactor = 1f - data.wardenDamageReduction;

            for (int i = 0; i < balls.Count; i++)
            {
                BallDestructionInfo b = balls[i];
                // Base ball damage is shielded; the enemy clear-bonus rides the same bolt unshielded
                // (it's the reward for clearing the enemy, not generic ball-clear damage).
                float dmg = b.damageValue * shieldFactor;
                if (b.enemyType != EnemyType.None && data != null)
                    dmg += data.GetClearBonus(b.enemyType);

                if (dmg <= 0f) continue;
                if (useDamageBolts) SpawnBolt(b.position, dmg);
                else currentBoss.TakeDamage(dmg);
            }
        }

        // Standard damage bolt — lands, deals its damage, recycles (boltArrivedCb).
        private void SpawnBolt(Vector3 origin, float dmg) => SpawnBolt(origin, dmg, boltArrivedCb);

        // Spawns a bolt with a custom arrival callback (e.g. the wave bolt, which also gates the
        // next-wave spawn on arrival). Same spawn mechanics; only the on-arrive behaviour differs.
        private void SpawnBolt(Vector3 origin, float dmg, System.Action<BossDamageBolt, float> onArrive)
        {
            if (currentBoss == null) return;
            BossDamageBolt bolt = AcquireBolt();
            activeBolts.Add(bolt);
            bolt.Launch(origin, currentBoss.transform, dmg,
                damageBoltSpeed, damageBoltArrivalDistance, onArrive, boltLostCb);
        }

        private void BoltArrived(BossDamageBolt bolt, float dmg)
        {
            // The boss may already be gone (an earlier bolt was lethal) — then drop the damage.
            if (currentBoss != null)
                currentBoss.TakeDamage(dmg);
            if (damageFlashRoutine == null) damageFlashRoutine = StartCoroutine(BossDamageRoutine(0.3f));;
            ReleaseBolt(bolt);
        }

        private System.Collections.IEnumerator BossDamageRoutine(float duration)
        {
            float half = Mathf.Max(0.01f, duration * 0.5f);

            Color bossColor = currentBoss.bossMaterial.color;
            float originalAlpha = bossColor.a;
            Color wardenColor = currentBoss.wardenBarrierMaterial.color;
            float targetAlpha = 0.6f; // How transparent the boss becomes
            float alpha;
            float t = 0f;
            if (AnyWardenAlive())
            {
                while (t < half)
                {
                    t += Time.deltaTime;
                    alpha = Mathf.Lerp(originalAlpha, targetAlpha, t / half);
                    bossColor.a = alpha;
                    currentBoss.bossMaterial.color = bossColor;
                    wardenColor.a = 1-alpha;
                    currentBoss.wardenBarrierMaterial.color = wardenColor;
                    

                    yield return null;
                }

                t = 0f;
                while (t < half)
                {
                    t += Time.deltaTime;
                    alpha = Mathf.Lerp(targetAlpha, originalAlpha, t / half);
                    bossColor.a = alpha;
                    currentBoss.bossMaterial.color = bossColor;
                    wardenColor.a = 1-alpha;
                    currentBoss.wardenBarrierMaterial.color = wardenColor;

                    yield return null;
                }
            }
            else
            {
                while (t < half)
                {
                    t += Time.deltaTime;
                    
                    bossColor.a = Mathf.Lerp(originalAlpha, targetAlpha, t / half);
                    currentBoss.bossMaterial.color = bossColor;

                    yield return null;
                }

                t = 0f;
                while (t < half)
                {
                    t += Time.deltaTime;

                    bossColor.a = Mathf.Lerp(targetAlpha, originalAlpha, t / half);
                    currentBoss.bossMaterial.color = bossColor;

                    yield return null;
                }
            }
            

            bossColor.a = originalAlpha;
            wardenColor.a = 0f;
            currentBoss.bossMaterial.color = bossColor;
            currentBoss.wardenBarrierMaterial.color = wardenColor;
            damageFlashRoutine = null;
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
            // just below the boss so it visibly rises into it when no fallback was set.
            Vector3 origin = fallbackDamageOrigin != Vector3.zero
                ? fallbackDamageOrigin
                : currentBoss.transform.position + Vector3.down * 4f;

            // Reuses the shared spawn path; only the arrival differs (it gates the next wave).
            SpawnBolt(origin, waveDamage, (b, dmg) => WaveBoltArrived(b, dmg, onBossSurvived));
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
