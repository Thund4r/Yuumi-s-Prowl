using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Simple UI bar driven by a RageMeter. While filling, the bar fills 0→1 from
    /// destroyed balls. When full, a prompt label appears above the bar telling the
    /// player which key activates rage. While active, the bar drains 1→0 across the
    /// active duration. Tints differently in each state.
    ///
    /// Setup:
    ///   1. Build a UI hierarchy on a Canvas: background Image + foreground (fill) Image.
    ///   2. Set the foreground Image's Image Type to Filled (Horizontal or Vertical).
    ///   3. (Optional) Add a TMP_Text above the bar for the "Press X to activate" prompt
    ///      and wire it into activationPromptLabel. Optionally also wire activationButton
    ///      so mobile / touch users can tap to activate.
    ///   4. Attach this component, wire the RageMeter and the foreground Image.
    /// </summary>
    public class RageMeterUI : MonoBehaviour
    {
        [SerializeField] private RageMeter rageMeter;
        [SerializeField] private Image fillImage;

        [Header("Colours")]
        [SerializeField] private Color fillingColor = new Color(0.6f, 0.3f, 1f);
        [SerializeField] private Color readyColor = new Color(1f, 0.7f, 1f);
        [SerializeField] private Color activeColor = new Color(1f, 0.4f, 1f);

        [Header("Activation Prompt (optional)")]
        [Tooltip("TMP text shown above the bar when the meter is full and waiting for activation. Hidden otherwise.")]
        [SerializeField] private TMP_Text activationPromptLabel;
        [Tooltip("Format string for the prompt — {0} is replaced with the RageMeter.ActivationKey name. E.g. 'Press {0} to RAGE'.")]
        [SerializeField] private string promptFormat = "Press {0} to RAGE";
        [Tooltip("Optional clickable button (mobile / touch). Wires OnClick → RageMeter.TryActivateRage().")]
        [SerializeField] private Button activationButton;

        [Tooltip("Optional — root GameObject toggled off while the meter is locked (no purple synergy yet).")]
        [SerializeField] private GameObject visibleRoot;

        private bool buttonHooked;

        private void OnEnable()
        {
            if (rageMeter != null)
            {
                rageMeter.OnRageChanged += HandleRageChanged;
                rageMeter.OnRageActivated += HandleRageActivated;
                rageMeter.OnRageExpired += HandleRageExpired;
                rageMeter.OnRageReady += HandleRageReady;
            }

            if (activationButton != null && rageMeter != null && !buttonHooked)
            {
                activationButton.onClick.AddListener(OnActivationButtonClicked);
                buttonHooked = true;
            }

            ApplyVisibility();
            RefreshFill();
            // Re-sync the prompt in case the meter is already in a ready state at enable time.
            UpdateActivationPrompt();
        }

        private void OnDisable()
        {
            if (rageMeter != null)
            {
                rageMeter.OnRageChanged -= HandleRageChanged;
                rageMeter.OnRageActivated -= HandleRageActivated;
                rageMeter.OnRageExpired -= HandleRageExpired;
                rageMeter.OnRageReady -= HandleRageReady;
            }

            if (activationButton != null && buttonHooked)
            {
                activationButton.onClick.RemoveListener(OnActivationButtonClicked);
                buttonHooked = false;
            }
        }

        private void Update()
        {
            // Visibility can change between floors as the player draws purple upgrades.
            ApplyVisibility();

            // Drain bar over the active duration.
            if (rageMeter != null && rageMeter.IsActive && fillImage != null && rageMeter.ActiveDurationTotal > 0f)
            {
                fillImage.fillAmount = Mathf.Clamp01(rageMeter.ActiveTimeRemaining / rageMeter.ActiveDurationTotal);
            }
        }

        private void HandleRageChanged(float current, float max)
        {
            // Only filling-state changes route here; active drain is handled in Update.
            if (rageMeter == null || rageMeter.IsActive) return;
            RefreshFill();
            // A change can drop us out of ready (e.g. refund on round-end leaves the bar
            // short of full) — keep the prompt in sync.
            UpdateActivationPrompt();
        }

        private void HandleRageReady()
        {
            if (fillImage != null) fillImage.color = readyColor;
            UpdateActivationPrompt();
        }

        private void HandleRageActivated()
        {
            if (fillImage != null)
            {
                fillImage.color = activeColor;
                fillImage.fillAmount = 1f;
            }
            UpdateActivationPrompt();
        }

        private void HandleRageExpired()
        {
            if (fillImage != null)
            {
                fillImage.color = fillingColor;
                fillImage.fillAmount = rageMeter != null ? rageMeter.Normalized : 0f;
            }
            UpdateActivationPrompt();
        }

        private void RefreshFill()
        {
            if (rageMeter == null || fillImage == null) return;
            fillImage.color = rageMeter.IsReady ? readyColor : fillingColor;
            fillImage.fillAmount = rageMeter.Normalized;
        }

        /// <summary>
        /// Shows the prompt when the meter is ready, hides it otherwise. Also toggles the
        /// activation button's interactivity in lockstep.
        /// </summary>
        private void UpdateActivationPrompt()
        {
            bool ready = rageMeter != null && rageMeter.IsReady;

            if (activationPromptLabel != null)
            {
                if (ready)
                {
                    activationPromptLabel.text = string.Format(promptFormat, rageMeter.ActivationKey.ToString());
                    activationPromptLabel.gameObject.SetActive(true);
                }
                else
                {
                    activationPromptLabel.gameObject.SetActive(false);
                }
            }

            if (activationButton != null)
            {
                activationButton.gameObject.SetActive(ready);
                activationButton.interactable = ready;
            }
        }

        private void OnActivationButtonClicked()
        {
            if (rageMeter != null) rageMeter.TryActivateRage();
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
