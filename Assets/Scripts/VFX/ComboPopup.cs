using System.Collections;
using TMPro;
using UnityEngine;

namespace YuumisProwl.VFX
{
    /// <summary>
    /// World-space floating text used by MatchEffectPlayer to show "+N" or
    /// "+N Combo xK" at the centroid of a match. Animates upward, scales, and fades
    /// then self-deactivates so the pool can reclaim it.
    /// </summary>
    public class ComboPopup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text label;

        [Header("Animation")]
        [SerializeField] private float duration = 0.8f;
        [SerializeField] private float floatDistance = 1.5f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 1.4f);
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        public float Duration => duration;

        public void Play(string text, Color color, Vector3 worldPos)
        {
            transform.position = worldPos;
            transform.localScale = Vector3.one;

            if (label != null)
            {
                label.text = text;
                label.color = color;
            }

            StopAllCoroutines();
            StartCoroutine(Animate(worldPos));
        }

        private IEnumerator Animate(Vector3 start)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                transform.position = start + Vector3.up * floatDistance * t;
                float s = scaleCurve.Evaluate(t);
                transform.localScale = new Vector3(s, s, s);

                if (label != null)
                {
                    Color c = label.color;
                    c.a = alphaCurve.Evaluate(t);
                    label.color = c;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
