using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YuumisProwl;
using YuumisProwl.BallChain;
using YuumisProwl.Utilities;

namespace YuumisProwl.VFX
{
    /// <summary>
    /// Subscribes to MatchProcessor.OnMatchVisual and plays per-ball destruction
    /// particles plus a combo popup at the match centroid. Both prefabs are pooled.
    /// </summary>
    public class MatchEffectPlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private BallDestructionEffect destructionEffectPrefab;
        [SerializeField] private ComboPopup comboPopupPrefab;

        [Header("Pool Sizes")]
        [SerializeField] private int initialEffectPoolSize = 16;
        [SerializeField] private int initialPopupPoolSize = 4;

        [Header("Combo")]
        [Tooltip("Cascade index (0 = initial match) at which the popup starts showing 'Combo xK'. " +
                 "Below this only '+N' is shown.")]
        [SerializeField] private int comboLabelMinCascade = 1;

        [Header("Gold Popup")]
        [Tooltip("Color of the floating gold-earned popup shown after a cascade sequence.")]
        [SerializeField] private Color goldColor = new Color(1f, 0.78f, 0.2f);
        [Tooltip("World-space offset applied to the gold popup relative to the last match centroid, so it doesn't overlap the combo text.")]
        [SerializeField] private Vector3 goldPopupOffset = new Vector3(0f, -0.6f, 0f);

        private ObjectPool<BallDestructionEffect> effectPool;
        private ObjectPool<ComboPopup> popupPool;

        // Centroid of the most recent match — used to position the post-cascade gold popup.
        private Vector3 lastMatchCentroid;

        private void Awake()
        {
            if (destructionEffectPrefab != null)
                effectPool = new ObjectPool<BallDestructionEffect>(destructionEffectPrefab, initialEffectPoolSize, transform);
            if (comboPopupPrefab != null)
                popupPool = new ObjectPool<ComboPopup>(comboPopupPrefab, initialPopupPoolSize, transform);
        }

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
            if (positions == null || positions.Count == 0) return;
            Color tint = BallColorUtils.ToUnityColor(color);

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < positions.Count; i++) centroid += positions[i];
            centroid /= positions.Count;
            lastMatchCentroid = centroid;

            if (effectPool != null)
            {
                var effect = effectPool.Get();
                effect.PlayEffect(centroid, tint);
                StartCoroutine(ReturnEffectAfter(effect));
            }

            if (popupPool != null)
            {
                string text = cascadeIndex >= comboLabelMinCascade
                    ? $"{cascadeIndex + 1}x Combo"
                    : $"";

                var popup = popupPool.Get();
                popup.Play(text, tint, centroid);
                StartCoroutine(ReturnPopupAfter(popup));
            }
        }

        /// <summary>
        /// Shows a floating "+N Gold" popup at the most recent match centroid.
        /// Called by RunManager after a cascade sequence awards gold.
        /// </summary>
        public void ShowGoldPopup(int goldAmount)
        {
            if (popupPool == null || goldAmount <= 0) return;

            var popup = popupPool.Get();
            popup.Play($"+{goldAmount} Gold", goldColor, lastMatchCentroid + goldPopupOffset);
            StartCoroutine(ReturnPopupAfter(popup));
        }

        private IEnumerator ReturnEffectAfter(BallDestructionEffect effect)
        {
            // BallDestructionEffect Invokes its own DisableEffect after effectDuration,
            // so we wait a touch longer to be safe before returning to the pool.
            yield return new WaitForSeconds(1.2f);
            if (effectPool != null) effectPool.Return(effect);
        }

        private IEnumerator ReturnPopupAfter(ComboPopup popup)
        {
            yield return new WaitForSeconds(popup.Duration);
            if (popupPool != null) popupPool.Return(popup);
        }
    }
}
