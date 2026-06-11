using System.Collections.Generic;
using UnityEngine;
using YuumisProwl;
using YuumisProwl.BallChain;
using YuumisProwl.Enemy;
using YuumisProwl.Managers;
using YuumisProwl.VFX;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Green poison synergy. Gated by RuntimeStats.PoisonEnabled (the Poison anchor upgrade).
    /// Each green ball destroyed (by any means — via the unified OnBallsDestroyed) flies a green
    /// PoisonBolt to the boss; on arrival it adds poison stacks. Poison ticks DoT to the boss every
    /// interval, and a single expiry timer — refreshed each time stacks are added — drops ALL stacks
    /// at once when it lapses, so the player sustains by feeding green. More green synergy upgrades
    /// lengthen the duration (easier to keep the stack alive). Mirrors IceSynergy's bolt-pool shape.
    /// </summary>
    public class PoisonSynergy : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private BossManager bossManager;
        [SerializeField] private RuntimeStats runtimeStats;
        [Tooltip("Prefab for the green poison bolt (must have a PoisonBolt component). If null, a green placeholder is created at runtime.")]
        [SerializeField] private PoisonBolt poisonBoltPrefab;
        [SerializeField, Min(1)] private int initialBoltPoolSize = 12;

        [Header("Poison Tuning")]
        [Tooltip("Poison stacks added per green ball destroyed.")]
        [SerializeField, Min(1)] private int stacksPerGreenBall = 1;
        [Tooltip("Seconds between poison ticks.")]
        [SerializeField, Min(0.05f)] private float tickInterval = 0.5f;
        [Tooltip("Boss damage per stack, per tick (linear). Total tick damage = stacks × this.")]
        [SerializeField, Min(0f)] private float damagePerStack = 0.5f;
        [Tooltip("Base seconds before all stacks expire (refreshed each time poison is added).")]
        [SerializeField, Min(0.1f)] private float baseDuration = 4f;
        [Tooltip("Extra duration per green synergy upgrade owned — more green makes stacks last longer (easier to sustain).")]
        [SerializeField, Min(0f)] private float durationPerGreenUpgrade = 1f;
        [Tooltip("Minimum tick interval after Rapid Decay reductions — ticks can't get faster than this.")]
        [SerializeField, Min(0.02f)] private float minTickInterval = 0.1f;

        [Header("Virulence (potency ramp)")]
        [Tooltip("Every this many stacks adds one virulence step to the tick-damage multiplier.")]
        [SerializeField, Min(1)] private int virulenceStackInterval = 5;
        [Tooltip("Multiplier added per virulence step. e.g. 0.5 → +50% tick damage per interval of stacks.")]
        [SerializeField, Min(0f)] private float virulenceBonusPerStep = 0.5f;
        [Tooltip("Cap on the virulence multiplier (1 = no ramp). 0 = uncapped.")]
        [SerializeField, Min(0f)] private float virulenceMaxMultiplier = 0f;

        [Header("Lingering Toxin (gradual decay)")]
        [Tooltip("Seconds between decay steps once poison has expired (Lingering Toxin only).")]
        [SerializeField, Min(0.05f)] private float lingerDecayInterval = 1f;
        [Tooltip("Fraction of stacks removed per decay step. e.g. 0.5 = halve each step.")]
        [Range(0f, 1f)] [SerializeField] private float lingerDecayFraction = 0.5f;

        [Header("Bolt Flight")]
        [SerializeField, Min(0.1f)] private float boltSpeed = 14f;
        [SerializeField, Min(0.05f)] private float boltArrivalDistance = 0.5f;

        private int stacks;
        private float expiryTimer;
        private float tickTimer;
        private float decayTimer;
        private float lastDuration;   // EffectiveDuration() at the last feed — denominator for the dial

        /// <summary>True while the boss is poisoned (stacks &gt; 0).</summary>
        public bool HasPoison => stacks > 0;

        /// <summary>Current poison stack count, for the stack indicator.</summary>
        public int PoisonStacks => stacks;

        /// <summary>
        /// Fill fraction (0–1) for the boss dial. In the normal window it's the remaining refresh
        /// duration (1 right after a feed, draining to 0). During Lingering Toxin's decay phase it
        /// switches to the decay-interval clock — so each time it empties (and the stack halves) it
        /// refills and drains again over lingerDecayInterval. 0 when unpoisoned.
        /// </summary>
        public float PoisonDurationFraction
        {
            get
            {
                if (stacks <= 0) return 0f;
                if (expiryTimer > 0f)
                    return lastDuration > 0f ? Mathf.Clamp01(expiryTimer / lastDuration) : 0f;
                // Expired: if Lingering Toxin is on, the dial becomes the decay-cycle clock.
                if (runtimeStats != null && runtimeStats.PoisonLingerEnabled && lingerDecayInterval > 0f)
                    return Mathf.Clamp01(decayTimer / lingerDecayInterval);
                return 0f;
            }
        }

        private readonly Queue<PoisonBolt> boltPool = new Queue<PoisonBolt>(16);
        private readonly List<PoisonBolt> activeBolts = new List<PoisonBolt>(16);
        private System.Action<PoisonBolt, int> boltArrivedCb;
        private System.Action<PoisonBolt> boltLostCb;

        private bool PoisonActive => runtimeStats != null && runtimeStats.PoisonEnabled;

        private void Start()
        {
            boltArrivedCb = OnPoisonBoltArrived;
            boltLostCb = ReleaseBolt;
            for (int i = 0; i < initialBoltPoolSize; i++)
                boltPool.Enqueue(CreateBolt());
        }

        private void OnEnable()
        {
            if (ballChainManager != null)
                ballChainManager.OnBallsDestroyed += HandleBallsDestroyed;
        }

        private void OnDisable()
        {
            if (ballChainManager != null)
                ballChainManager.OnBallsDestroyed -= HandleBallsDestroyed;
            ResetPoison();
        }

        private void Update()
        {
            if (stacks <= 0) return;

            // Boss gone (defeated / between floors) — poison can't persist across bosses.
            Boss boss = bossManager != null ? bossManager.CurrentBoss : null;
            if (boss == null) { ResetPoison(); return; }

            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                boss.TakeDamage(stacks * damagePerStack * VirulenceMultiplier());
                tickTimer += EffectiveTickInterval();
            }

            // Duration window, then (with Lingering Toxin) a repeating decay clock. Each time the
            // active clock empties, the stack halves and the dial refills for the next decay interval.
            if (expiryTimer > 0f)
            {
                expiryTimer -= Time.deltaTime;
                if (expiryTimer <= 0f)
                {
                    if (Lingering)
                    {
                        HalveStacks();                    // halve as the duration empties...
                        decayTimer = lingerDecayInterval; // ...then refill the dial for the decay cycle
                    }
                    else
                    {
                        stacks = 0; // default: the whole stack expires at once — feed green to keep it alive
                    }
                }
            }
            else if (Lingering && stacks > 0)
            {
                decayTimer -= Time.deltaTime;
                if (decayTimer <= 0f)
                {
                    HalveStacks();                    // halve each time the decay clock empties...
                    decayTimer = lingerDecayInterval; // ...and refill again
                }
            }
        }

        private bool Lingering => runtimeStats != null && runtimeStats.PoisonLingerEnabled;

        private void HalveStacks()
        {
            stacks = Mathf.FloorToInt(stacks * (1f - lingerDecayFraction));
        }

        /// <summary>Tick-damage multiplier from Virulence: ramps with stack count once unlocked.</summary>
        private float VirulenceMultiplier()
        {
            if (runtimeStats == null || !runtimeStats.PoisonVirulenceEnabled || virulenceStackInterval <= 0)
                return 1f;
            float mult = 1f + (stacks / virulenceStackInterval) * virulenceBonusPerStep; // integer ⌊stacks/X⌋
            if (virulenceMaxMultiplier > 0f) mult = Mathf.Min(mult, virulenceMaxMultiplier);
            return mult;
        }

        /// <summary>Tick interval after Rapid Decay reductions, floored.</summary>
        private float EffectiveTickInterval()
        {
            float reduction = runtimeStats != null ? runtimeStats.PoisonTickReduction : 0f;
            return Mathf.Max(minTickInterval, tickInterval - reduction);
        }

        /// <summary>Stacks applied per green ball, including Heavy Dose.</summary>
        private int StacksPerGreen()
        {
            int bonus = runtimeStats != null ? runtimeStats.PoisonStacksBonus : 0;
            return Mathf.Max(1, stacksPerGreenBall + bonus);
        }

        private void HandleBallsDestroyed(List<BallDestructionInfo> balls)
        {
            if (!PoisonActive || bossManager == null || bossManager.CurrentBoss == null) return;

            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i].color != BallColor.Green) continue;
                if (balls[i].enemyType == EnemyType.Stone) continue; // colourless wall — its colour is a placeholder
                SpawnPoisonBolt(balls[i].position);
            }
        }

        private void SpawnPoisonBolt(Vector3 origin)
        {
            Boss boss = bossManager.CurrentBoss;
            if (boss == null) return;
            PoisonBolt bolt = AcquireBolt();
            activeBolts.Add(bolt);
            bolt.Launch(origin, boss.transform, StacksPerGreen(), boltSpeed, boltArrivalDistance, boltArrivedCb, boltLostCb);
        }

        private void OnPoisonBoltArrived(PoisonBolt bolt, int stackCount)
        {
            AddPoison(stackCount);
            ReleaseBolt(bolt);
        }

        private void AddPoison(int amount)
        {
            if (amount <= 0) return;
            if (stacks <= 0) tickTimer = EffectiveTickInterval(); // start the tick cadence on the first application
            stacks += amount;
            lastDuration = EffectiveDuration();
            expiryTimer = lastDuration;        // refresh-on-feed
            decayTimer = lingerDecayInterval;  // reset the Lingering Toxin decay clock
        }

        private float EffectiveDuration()
        {
            int greenCount = runtimeStats != null ? runtimeStats.GetColorSynergyCount(BallColor.Green) : 0;
            return baseDuration + greenCount * durationPerGreenUpgrade;
        }

        private void ResetPoison()
        {
            stacks = 0;
            expiryTimer = 0f;
            tickTimer = 0f;
            decayTimer = 0f;
            lastDuration = 0f;
            ReleaseAllBolts();
        }

        // --------------------------------------------------------------
        // Poison-bolt pool
        // --------------------------------------------------------------

        private PoisonBolt AcquireBolt()
        {
            return boltPool.Count > 0 ? boltPool.Dequeue() : CreateBolt();
        }

        private PoisonBolt CreateBolt()
        {
            PoisonBolt bolt;
            if (poisonBoltPrefab != null)
            {
                bolt = Instantiate(poisonBoltPrefab, transform);
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform);
                go.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.3f, 1f, 0.3f);
                bolt = go.AddComponent<PoisonBolt>();
                go.name = "PoisonBolt_Placeholder";
            }
            bolt.gameObject.SetActive(false);
            return bolt;
        }

        private void ReleaseBolt(PoisonBolt bolt)
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
