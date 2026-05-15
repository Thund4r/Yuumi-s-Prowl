using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Displays the upgrade draft screen after a level win.
    /// Shows 3 random upgrade options; player picks one to proceed to the next level.
    /// Supports reroll if the player has draft reroll meta upgrades.
    /// </summary>
    public class UpgradeDraftUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button[] optionButtons = new Button[3];
        [SerializeField] private Image[] optionIcons = new Image[3];
        [SerializeField] private TextMeshProUGUI[] optionNames = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] optionDescriptions = new TextMeshProUGUI[3];

        [Header("Reroll")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private TextMeshProUGUI rerollCountText;

        [SerializeField] private float fadeDuration = 0.3f;

        private UpgradeDefinition[] currentOptions = new UpgradeDefinition[3];
        private Action<UpgradeDefinition> onUpgradeSelected;
        private Func<UpgradeDefinition[]> onRerollRequested;
        private int rerollsRemaining = 0;
        private bool isWaitingForSelection = false;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            // Immediately hide without deactivating — keeps the GameObject alive so
            // Awake can finish wiring listeners, but ensures the panel doesn't render.
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                optionButtons[i].onClick.AddListener(() => SelectUpgrade(index));
            }

            if (rerollButton != null)
                rerollButton.onClick.AddListener(OnRerollClicked);
        }

        /// <summary>
        /// Displays the draft screen with the given upgrade options.
        /// Calls onSelected when the player picks one.
        /// Pass a rerollGenerator to enable rerolls; pass rerollCount > 0 to allow them.
        /// </summary>
        public void Show(UpgradeDefinition[] options, Action<UpgradeDefinition> onSelected,
                         int rerollCount = 0, Func<UpgradeDefinition[]> rerollGenerator = null)
        {
            if (options == null || options.Length != 3)
            {
                Debug.LogError("UpgradeDraftUI: Must provide exactly 3 upgrade options.");
                return;
            }

            onUpgradeSelected = onSelected;
            onRerollRequested = rerollGenerator;
            rerollsRemaining = rerollCount;

            SetOptions(options);
            UpdateRerollDisplay();

            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            isWaitingForSelection = true;

            StartCoroutine(FadeIn());
        }

        private void SetOptions(UpgradeDefinition[] options)
        {
            currentOptions = options;

            for (int i = 0; i < 3; i++)
            {
                var upgrade = options[i];
                if (optionIcons[i] != null && upgrade.Icon != null)
                    optionIcons[i].sprite = upgrade.Icon;

                if (optionNames[i] != null)
                    optionNames[i].text = upgrade.UpgradeName;

                if (optionDescriptions[i] != null)
                    optionDescriptions[i].text = upgrade.Description;

                optionButtons[i].interactable = true;
            }
        }

        private void UpdateRerollDisplay()
        {
            if (rerollButton != null)
            {
                rerollButton.gameObject.SetActive(onRerollRequested != null);
                rerollButton.interactable = rerollsRemaining > 0;
            }

            if (rerollCountText != null)
                rerollCountText.text = $"Rerolls: {rerollsRemaining}";
        }

        private void OnRerollClicked()
        {
            if (rerollsRemaining <= 0 || onRerollRequested == null)
                return;

            var newOptions = onRerollRequested.Invoke();
            if (newOptions == null || newOptions.Length != 3)
            {
                Debug.LogError("UpgradeDraftUI: Reroll generator returned invalid options.");
                return;
            }

            rerollsRemaining--;
            SetOptions(newOptions);
            UpdateRerollDisplay();
        }

        private void SelectUpgrade(int index)
        {
            if (!isWaitingForSelection || index < 0 || index >= currentOptions.Length)
                return;

            isWaitingForSelection = false;
            var selected = currentOptions[index];

            foreach (var btn in optionButtons)
                btn.interactable = false;

            if (rerollButton != null)
                rerollButton.interactable = false;

            StartCoroutine(FadeOut(() => onUpgradeSelected?.Invoke(selected)));
        }

        private IEnumerator FadeIn()
        {
            canvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut(Action onComplete)
        {
            canvasGroup.alpha = 1f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            onComplete?.Invoke();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < optionButtons.Length; i++)
                optionButtons[i].onClick.RemoveAllListeners();

            if (rerollButton != null)
                rerollButton.onClick.RemoveAllListeners();
        }
    }
}
