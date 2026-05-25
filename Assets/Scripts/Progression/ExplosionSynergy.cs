using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Red colour synergy: when a red match meets the size threshold, an explosion goes
    /// off at the match site, destroying nearby balls. The explosion radius is
    /// RuntimeStats.ExplosionRadius (count-scaled by red synergy upgrades).
    ///
    /// Subscribes to MatchProcessor.OnMatchVisual. OnMatchVisual fires *before* RemoveBalls,
    /// mid-match-processing — so the explosion is deferred one frame to avoid mutating the
    /// chain re-entrantly. Cleared/cascaded the same way the Bomb power-up is.
    ///
    /// Setup: add to a GameObject in the Game scene; wire MatchProcessor, BallChainManager,
    /// RuntimeStats and RunConfig.
    /// </summary>
    public class ExplosionSynergy : MonoBehaviour
    {
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private RuntimeStats runtimeStats;
        [SerializeField] private RunConfig config;

        private void OnEnable()
        {
            if (matchProcessor != null)
                matchProcessor.OnMatchVisual += HandleMatchVisual;
        }

        private void OnDisable()
        {
            if (matchProcessor != null)
                matchProcessor.OnMatchVisual -= HandleMatchVisual;
        }

        private void HandleMatchVisual(List<Vector3> positions, BallColor color, int cascadeIndex)
        {
            if (runtimeStats == null || !runtimeStats.RedMatchExplosionEnabled) return;
            if (color != BallColor.Red) return;
            if (positions == null || positions.Count == 0) return;
            if (positions.Count < GetEffectiveThreshold()) return;

            // Centroid of the matched balls.
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < positions.Count; i++) centroid += positions[i];
            centroid /= positions.Count;

            // Defer one frame — OnMatchVisual fires mid-match-processing (before RemoveBalls);
            // exploding now would mutate the chain re-entrantly.
            StartCoroutine(ExplodeNextFrame(centroid));
        }

        /// <summary>
        /// Effective red-match size needed to explode: the RunConfig base minus any
        /// ExplosionThresholdReduction upgrades, floored at 3.
        /// </summary>
        private int GetEffectiveThreshold()
        {
            int baseThreshold = config != null ? config.redMatchExplosionThreshold : 4;
            int reduction = runtimeStats != null ? runtimeStats.ExplosionThresholdReduction : 0;
            return Mathf.Max(3, baseThreshold - reduction);
        }

        private IEnumerator ExplodeNextFrame(Vector3 center)
        {
            yield return null;
            ExplodeAt(center);
        }

        private void ExplodeAt(Vector3 center)
        {
            if (ballChainManager == null) return;

            float radius = runtimeStats != null ? runtimeStats.ExplosionRadius : 3f;
            if (radius <= 0f) return;

            Collider[] hits = Physics.OverlapSphere(center, radius);
            List<int> indices = new List<int>();
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Ball")) continue;
                Ball ball = hit.GetComponent<Ball>();
                if (ball != null && !indices.Contains(ball.ChainIndex))
                    indices.Add(ball.ChainIndex);
            }

            if (indices.Count == 0) return;

            // Remove highest indices first so lower indices stay valid.
            indices.Sort((a, b) => b.CompareTo(a));
            for (int i = 0; i < indices.Count; i++)
                ballChainManager.RemoveBallAtIndex(indices[i]);

            // Hand off to MatchProcessor for cascades + chain-cleared detection — same
            // path the Bomb power-up uses.
            if (matchProcessor != null)
                matchProcessor.ProcessPierceAftermath(indices.Count);

            Debug.Log($"ExplosionSynergy: red-match explosion destroyed {indices.Count} balls (radius {radius:F1}).");
        }
    }
}
