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
    }
}
