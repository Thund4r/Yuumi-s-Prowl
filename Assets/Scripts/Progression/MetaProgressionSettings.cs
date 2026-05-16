using UnityEngine;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Tunable configuration for end-of-run essence rewards.
    ///
    /// Meta upgrades themselves are no longer defined here — they are ordinary
    /// UpgradeDefinition assets with IsMetaShop enabled (see UpgradeDefinition).
    ///
    /// Create assets via: Right-click in Project → Yuumi → Meta Progression Settings
    /// </summary>
    [CreateAssetMenu(fileName = "MetaProgressionSettings", menuName = "Yuumi/Meta Progression Settings")]
    public class MetaProgressionSettings : ScriptableObject
    {
        [Header("Essence Rewards")]
        [Tooltip("Base essence awarded per floor cleared.")]
        [Min(1)] public int baseEssencePerFloor = 10;

        [Tooltip("Curve multiplier applied to essence based on floor progress (0 at first, 1 at last). Empty = constant 1.0.")]
        public AnimationCurve essenceDepthCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("If enabled, essence is multiplied by (ballSpeedMult * totalBallsMult) for difficulty scaling.")]
        public bool essenceDifficultyScaling = true;

        /// <summary>
        /// Safe sampler for the essence depth curve.
        /// Returns 1.0 if the curve is empty (unconfigured).
        /// </summary>
        public float SampleEssenceDepthMult(float t)
        {
            if (essenceDepthCurve == null || essenceDepthCurve.length == 0)
                return 1f;
            return essenceDepthCurve.Evaluate(Mathf.Clamp01(t));
        }
    }
}
