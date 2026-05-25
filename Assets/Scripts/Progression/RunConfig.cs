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

        [Tooltip("Multiplier applied to LevelData.totalBalls at floor%. X=0 is the first floor, X=1 is the last. Empty curve = constant 1.0 (no scaling). Result is rounded and clamped to >= 1.")]
        public AnimationCurve totalBallsCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Gold Rewards")]
        [Tooltip("Base gold awarded per gameplay floor cleared.")]
        [Min(0)] public int baseGoldPerFloor = 30;

        [Tooltip("Bonus gold awarded per cascade in a match sequence (after the first match).")]
        [Min(0)] public int goldPerCascadeBonus = 5;

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

        [Header("Rage Synergy (Purple)")]
        [Tooltip("Purple synergy upgrades needed for rage to grant loose homing + multi-fire (instead of strict). Count includes the RageUnlock anchor itself.")]
        [Min(1)] public int rageLoosePurpleCount = 3;

        [Tooltip("Maximum value of the rage meter — once reached, rage activates.")]
        [Min(1f)] public float rageMeterMax = 100f;

        [Tooltip("Rage gained per ball destroyed (matches fill the meter).")]
        [Min(0f)] public float rageGainPerBall = 5f;

        [Tooltip("Base seconds rage stays active once triggered (before bonuses).")]
        [Min(0.1f)] public float rageDuration = 5f;

        /// <summary>
        /// Safe sampler — returns 1.0 when the curve has no keyframes so an unconfigured
        /// asset doesn't silently zero out gameplay values.
        /// </summary>
        public float SampleBallSpeedMult(float t)
        {
            if (ballSpeedCurve == null || ballSpeedCurve.length == 0) return 1f;
            return ballSpeedCurve.Evaluate(Mathf.Clamp01(t));
        }

        /// <summary>Safe sampler — see SampleBallSpeedMult.</summary>
        public float SampleTotalBallsMult(float t)
        {
            if (totalBallsCurve == null || totalBallsCurve.length == 0) return 1f;
            return totalBallsCurve.Evaluate(Mathf.Clamp01(t));
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
