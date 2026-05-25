using UnityEngine;
using UnityEngine.UI;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Simple UI bar driven by a RageMeter. While filling, the bar fills 0→1 from
    /// destroyed balls. While active, the bar drains 1→0 across the active duration.
    /// Tints differently in each state.
    ///
    /// Setup:
    ///   1. Build a UI hierarchy on a Canvas: background Image + foreground (fill) Image.
    ///   2. Set the foreground Image's Image Type to Filled (Horizontal or Vertical).
    ///   3. Attach this component, wire the RageMeter and the foreground Image.
    /// </summary>
    public class RageMeterUI : MonoBehaviour
    {
        [SerializeField] private RageMeter rageMeter;
        [SerializeField] private Image fillImage;

        [Header("Colours")]
        [SerializeField] private Color fillingColor = new Color(0.6f, 0.3f, 1f);
        [SerializeField] private Color activeColor = new Color(1f, 0.4f, 1f);

        [Tooltip("Optional — root GameObject toggled off while the meter is locked (no purple synergy yet).")]
        [SerializeField] private GameObject visibleRoot;

        // Captured at activation so we can show the drain as a 0–1 fraction.
        private float activeDurationSnapshot;

        private void OnEnable()
        {
            if (rageMeter != null)
            {
                rageMeter.OnRageChanged += HandleRageChanged;
                rageMeter.OnRageActivated += HandleRageActivated;
                rageMeter.OnRageExpired += HandleRageExpired;
            }
            ApplyVisibility();
            RefreshFill();
        }

        private void OnDisable()
        {
            if (rageMeter != null)
            {
                rageMeter.OnRageChanged -= HandleRageChanged;
                rageMeter.OnRageActivated -= HandleRageActivated;
                rageMeter.OnRageExpired -= HandleRageExpired;
            }
        }

        private void Update()
        {
            // Visibility can change between floors as the player draws purple upgrades.
            ApplyVisibility();

            // Drain bar over the active duration.
            if (rageMeter != null && rageMeter.IsActive && fillImage != null && activeDurationSnapshot > 0f)
            {
                fillImage.fillAmount = Mathf.Clamp01(rageMeter.ActiveTimeRemaining / activeDurationSnapshot);
            }
        }

        private void HandleRageChanged(float current, float max)
        {
            // Only filling-state changes route here; active drain is handled in Update.
            if (rageMeter == null || rageMeter.IsActive) return;
            RefreshFill();
        }

        private void HandleRageActivated()
        {
            activeDurationSnapshot = rageMeter != null ? rageMeter.ActiveTimeRemaining : 0f;
            if (fillImage != null)
            {
                fillImage.color = activeColor;
                fillImage.fillAmount = 1f;
            }
        }

        private void HandleRageExpired()
        {
            activeDurationSnapshot = 0f;
            if (fillImage != null)
            {
                fillImage.color = fillingColor;
                fillImage.fillAmount = 0f;
            }
        }

        private void RefreshFill()
        {
            if (rageMeter == null || fillImage == null) return;
            fillImage.color = fillingColor;
            fillImage.fillAmount = rageMeter.Normalized;
        }

        private void ApplyVisibility()
        {
            if (visibleRoot == null || rageMeter == null) return;
            // Guard: if the user wired visibleRoot to this script's own GameObject,
            // disabling it would also disable this Update — once locked we could never
            // re-enable. Skip the toggle in that case.
            if (visibleRoot == gameObject) return;
            visibleRoot.SetActive(rageMeter.IsUnlocked);
        }
    }
}
