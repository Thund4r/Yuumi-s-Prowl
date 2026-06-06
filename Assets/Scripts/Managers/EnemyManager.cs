using System.Collections.Generic;
using UnityEngine;
using YuumisProwl;

namespace YuumisProwl.Managers
{
    /// <summary>
    /// Plans which in-chain disruptor enemies get injected into each wave, reading the live boss's
    /// enemy spec (BossData.enemySpec). BallSpawner calls BuildWavePlan and spawns the planned
    /// enemy balls. The boss-HP effects of enemies — Warden shield, clear-bonus — live in
    /// BossManager; this component only handles spawn planning.
    /// </summary>
    public class EnemyManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BossManager bossManager;

        // Reused so repeated waves don't allocate a fresh slot list each time.
        private readonly List<int> slotBuffer = new List<int>();
        private readonly List<EnemyType> placeBuffer = new List<EnemyType>();

        /// <summary>
        /// Builds a per-slot plan for a wave of `waveCount` balls: index i holds the EnemyType to
        /// spawn at that slot (None = a normal ball). Returns null when there's no enemy spec, so
        /// callers can skip the per-slot branch entirely. Enemies are scattered to random distinct
        /// slots so they don't clump together.
        /// </summary>
        public EnemyType[] BuildWavePlan(int waveCount)
        {
            if (waveCount <= 0) return null;

            BossData data = bossManager != null ? bossManager.CurrentBossData : null;
            if (data == null || data.enemySpec == null || data.enemySpec.Length == 0) return null;

            // Flatten the spec into the list of enemies to place this wave.
            placeBuffer.Clear();
            for (int i = 0; i < data.enemySpec.Length; i++)
            {
                EnemySpawn entry = data.enemySpec[i];
                if (entry == null || entry.type == EnemyType.None || entry.perWave <= 0) continue;
                for (int n = 0; n < entry.perWave; n++)
                    placeBuffer.Add(entry.type);
            }
            if (placeBuffer.Count == 0) return null;

            EnemyType[] plan = new EnemyType[waveCount];   // all None by default

            // Partial Fisher-Yates over the slot indices: pick a distinct random slot per enemy.
            slotBuffer.Clear();
            for (int i = 0; i < waveCount; i++) slotBuffer.Add(i);

            int placeCount = Mathf.Min(placeBuffer.Count, waveCount);
            for (int i = 0; i < placeCount; i++)
            {
                int pick = Random.Range(i, slotBuffer.Count);
                int slot = slotBuffer[pick];
                slotBuffer[pick] = slotBuffer[i];
                slotBuffer[i] = slot;
                plan[slot] = placeBuffer[i];
            }
            return plan;
        }
    }
}
