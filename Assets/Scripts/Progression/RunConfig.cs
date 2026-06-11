using UnityEngine;
using YuumisProwl.Managers;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Authoring data for a run's shape.
    /// Create assets via: Right-click in Project → Yuumi → Run Config
    /// </summary>
    [CreateAssetMenu(fileName = "RunConfig", menuName = "Yuumi/Run Config")]
    public class RunConfig : ScriptableObject
    {
        [Header("Run Structure")]
        [Tooltip("Pool of maps a run can draw from. Random selection at run start.")]
        public Map[] mapPool;

        [Tooltip("How many gameplay floors per run. Random picks from mapPool.")]
        [Min(1)] public int mapCount = 8;

        [Tooltip("If true, the same map may appear multiple times in one run. If false, picks without replacement and refills the bag when exhausted.")]
        public bool allowDuplicates = true;

        [Header("Shops")]
        [Tooltip("After which gameplay floor indices (0-based) a shop appears. E.g., [2, 5] = shop after the 3rd and 6th gameplay floor.")]
        public int[] shopFloorIndices = new int[0];

        [Tooltip("Gold cost to reroll the shop (if the player has the ShopReroll upgrade).")]
        [Min(1)] public int shopRerollCost = 25;

        [Header("Difficulty Scaling")]
        [Tooltip("Multiplier applied to LevelData.ballSpeed at floor%. X=0 is the first floor, X=1 is the last. Empty curve = constant 1.0 (no scaling).")]
        public AnimationCurve ballSpeedCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Multiplier applied to LevelData.bossHealth at floor%. X=0 is the first floor, X=1 is the last. Empty curve = constant 1.0 (no scaling).")]
        public AnimationCurve bossHealthCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Gold Rewards")]
        [Tooltip("Base gold awarded per gameplay floor cleared.")]
        [Min(0)] public int baseGoldPerFloor = 30;

        [Tooltip("Bonus gold awarded per cascade in a match sequence (after the first match).")]
        [Min(0)] public int goldPerCascadeBonus = 5;

        [Header("Potions")]
        [Tooltip("Chance (0-1) to grant a random potion when a gameplay floor is cleared.")]
        [Range(0f, 1f)] public float potionRewardChance = 0.35f;

        [Tooltip("Gold cost of a potion in the in-run shop.")]
        [Min(0)] public int potionShopCost = 40;

        [Header("Explosion Synergy (Red)")]
        [Tooltip("Baseline explosion radius (Bomb power-up + red-match explosions) before any red synergy upgrades.")]
        [Min(0f)] public float baseExplosionRadius = 3f;

        [Tooltip("Extra explosion radius per red colour-synergy upgrade the player owns. Count-scaling balance knob.")]
        [Min(0f)] public float explosionRadiusPerRedUpgrade = 0.5f;

        [Tooltip("Minimum red match size that triggers an explosion (when the RedMatchExplosion upgrade is owned). Threshold-reduction upgrades lower this, floored at 3.")]
        [Min(3)] public int redMatchExplosionThreshold = 4;

        [Header("Colour Weighting")]
        [Tooltip("Extra spawn weight a colour gets per colour-synergy upgrade of that colour owned. Baseline weight is 1.0. So with 1.0 here, X red upgrades = +X red weight.")]
        [Min(0f)] public float colorWeightPerSynergyUpgrade = 1f;

        [Header("Ice Patches Synergy (Blue)")]
        [Tooltip("World-space radius of an ice patch dropped by a blue match.")]
        [Min(0.1f)] public float icePatchRadius = 2.5f;

        [Tooltip("How long an ice patch persists after a blue match drops it (seconds).")]
        [Min(0.1f)] public float icePatchDuration = 5f;

        [Tooltip("Freeze stacks a ball must accrue before becoming frozen. Stacks zero out on freeze.")]
        [Min(1)] public int iceFreezeStackThreshold = 3;

        [Tooltip("After a ball exits an ice patch, how long before that same patch can re-apply a stack to it. Prevents per-frame stack-spam on slow-moving / re-entering balls.")]
        [Min(0f)] public float icePatchReentryCooldown = 2f;

        [Tooltip("Travel speed of an icicle projectile spawned when a frozen ball is destroyed (world units / second).")]
        [Min(0.1f)] public float icicleSpeed = 8f;

        [Tooltip("Distance at which an icicle is considered to have arrived at its target ball (world units).")]
        [Min(0.05f)] public float icicleArrivalDistance = 0.4f;

        [Tooltip("World-space max radius of a cryo burst — the AoE ring that grows out from a blue match centroid.")]
        [Min(0.1f)] public float cryoBurstRadius = 2f;

        [Tooltip("How long the cryo burst ring takes to grow from 0 to cryoBurstRadius (seconds). Stacks are applied as the ring sweeps past each ball.")]
        [Min(0.05f)] public float cryoBurstDuration = 0.4f;

        [Tooltip("Base seconds the chain slowdown lasts after a blue match (BlueChainSlowdown). Stacks with BlueSlowdownDurationBonus from upgrades.")]
        [Min(0.1f)] public float blueSlowdownBaseDuration = 1f;

        [Tooltip("Speed multiplier subtracted per blue synergy upgrade owned during the chain slowdown. e.g. 0.1 means with 3 blue upgrades, chain runs at 1.0 - 3*0.1 = 0.7x normal speed.")]
        [Range(0f, 0.5f)] public float blueSlowdownPerUpgrade = 0.1f;

        [Tooltip("Minimum chain speed multiplier during slowdown — even with many blue upgrades, the chain can't go slower than this. Prevents totally freezing the chain.")]
        [Range(0.05f, 1f)] public float blueSlowdownMinMultiplier = 0.3f;

        [Header("Rage Synergy (Purple)")]
        [Tooltip("Purple synergy upgrades needed for rage to grant loose homing + multi-fire (instead of strict). Count includes the RageUnlock anchor itself.")]
        [Min(1)] public int rageLoosePurpleCount = 3;

        [Tooltip("Maximum value of the rage meter — once reached, rage activates.")]
        [Min(1f)] public float rageMeterMax = 100f;

        [Tooltip("Rage gained per ball destroyed (matches fill the meter).")]
        [Min(0f)] public float rageGainPerBall = 5f;

        [Tooltip("Base seconds rage stays active once triggered (before bonuses).")]
        [Min(0.1f)] public float rageDuration = 5f;

        [Header("Conductor Synergy (Orange)")]
        [Tooltip("Base number of balls an arc hops through before any orange synergy upgrades.")]
        [Min(1)] public int baseArcBounces = 3;

        [Tooltip("Extra arc hops per orange colour-synergy upgrade owned (count-scaling knob).")]
        [Min(0)] public int arcBouncesPerOrangeUpgrade = 1;

        [Tooltip("Maximum world-space distance an arc can hop from the current point to the next ball.")]
        [Min(0.1f)] public float arcRange = 3f;

        [Tooltip("Rage added per arc hop when charging a purple ball (scaled by arc charge units).")]
        [Min(0f)] public float arcRageGain = 5f;

        [Tooltip("Arc hops needed to prime a red ball (ignite threshold). A primed red leaves a mini-explosion when destroyed.")]
        [Min(1)] public int igniteThreshold = 3;

        [Tooltip("World-space radius of the mini-explosion a primed red leaves when destroyed. Tune well below explosion radius.")]
        [Min(0.1f)] public float igniteMiniRadius = 1f;

        [Tooltip("World-space radius of the merge for coalescing ignite triggers into one explosion when multiple primed reds detonate close together. Also used for merging multiple red-match explosion triggers. Tune well below explosion radius to avoid excessive merging.")]
        [Min(0.1f)] public float igniteMergeRadius = 0.5f;

        [Tooltip("Static stacks an arc must apply to a ball whose colour has no active synergy before it pops (baseline). Not upgradeable.")]
        [Min(1)] public int staticThreshold = 3;

        [Tooltip("Every Nth arc applies double charge when the Supercharge upgrade is owned.")]
        [Min(1)] public int superchargeEveryNth = 3;

        /// <summary>
        /// Safe sampler — returns 1.0 when the curve has no keyframes so an unconfigured
        /// asset doesn't silently zero out gameplay values.
        /// </summary>
        public float SampleBallSpeedMult(float t)
        {
            if (ballSpeedCurve == null || ballSpeedCurve.length == 0) return 1f;
            return ballSpeedCurve.Evaluate(Mathf.Clamp01(t));
        }

        public float SampleBossHealthMult(float t)
        {
            if (bossHealthCurve == null || bossHealthCurve.length == 0) return 1f;
            return bossHealthCurve.Evaluate(Mathf.Clamp01(t));
        }

        /// <summary>
        /// Returns true if a shop should appear after the given gameplay floor index.
        /// </summary>
        public bool HasShopAfterFloor(int gameplayFloorIndex)
        {
            if (shopFloorIndices == null) return false;
            for (int i = 0; i < shopFloorIndices.Length; i++)
            {
                if (shopFloorIndices[i] == gameplayFloorIndex) return true;
            }
            return false;
        }
    }
}
