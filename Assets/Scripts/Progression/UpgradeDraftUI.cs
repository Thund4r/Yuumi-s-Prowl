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
    /// </summary>
    public class UpgradeDraftUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button[] optionButtons = new Button[3];
        [SerializeField] private Image[] optionIcons = new Image[3];
        [SerializeField] private TextMeshProUGUI[] optionNames = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] optionDescriptions = new TextMeshProUGUI[3];

        [SerializeField] private float fadeDuration = 0.3f;

        private UpgradeDefinition[] currentOptions = new UpgradeDefinition[3];
        private Action<UpgradeDefinition> onUpgradeSelected;
        private bool isWaitingForSelection = false;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                optionButtons[i].onClick.AddListener(() => SelectUpgrade(index));
            }

            Hide();
        }

        /// <summary>
        /// Displays the draft screen with the given upgrade options.
        /// Calls onSelected when the player picks one.
        /// </summary>
        public void Show(UpgradeDefinition[] options, Action<UpgradeDefinition> onSelected)
        {
            if (options == null || options.Length != 3)
            {
                Debug.LogError("UpgradeDraftUI: Must provide exactly 3 upgrade options.");
                return;
            }

            currentOptions = options;
            onUpgradeSelected = onSelected;
            isWaitingForSelection = true;

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

            gameObject.SetActive(true);
            StartCoroutine(FadeIn());
        }

        private void SelectUpgrade(int index)
        {
            if (!isWaitingForSelection || index < 0 || index >= currentOptions.Length)
                return;

            isWaitingForSelection = false;
            var selected = currentOptions[index];

            foreach (var btn in optionButtons)
                btn.interactable = false;

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
            gameObject.SetActive(false);
            onComplete?.Invoke();
        }

        private void Hide()
        {
            gameObject.SetActive(false);
            canvasGroup.alpha = 0f;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < optionButtons.Length; i++)
                optionButtons[i].onClick.RemoveAllListeners();
        }
    }
}
