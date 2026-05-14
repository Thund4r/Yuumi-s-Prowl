using UnityEngine;
using YuumisProwl.Managers;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Authoring data for a run's shape. Step 2: just the map pool and run length.
    /// Future fields (added in later steps):
    ///   - shopFloors: int[]                    (step 6)
    ///   - ballSpeedCurve, totalBallsCurve      (step 3)
    ///   - commonPool, rarePool, shopPool       (steps 4 / 6)
    ///
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

        [Header("Difficulty Scaling")]
        [Tooltip("Multiplier applied to LevelData.ballSpeed at floor%. X=0 is the first floor, X=1 is the last. Empty curve = constant 1.0 (no scaling).")]
        public AnimationCurve ballSpeedCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Multiplier applied to LevelData.totalBalls at floor%. X=0 is the first floor, X=1 is the last. Empty curve = constant 1.0 (no scaling). Result is rounded and clamped to >= 1.")]
        public AnimationCurve totalBallsCurve = AnimationCurve.Constant(0f, 1f, 1f);

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
    }
}
