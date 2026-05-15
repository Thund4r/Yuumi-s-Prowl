using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// In-run shop UI: displays purchasable upgrades and gold balance.
    /// Card slots are placed manually in the scene/prefab and wired into the
    /// upgradeCards array; the shop populates them with data when it opens.
    /// On Continue, advances to the next run node.
    /// </summary>
    public class InRunShopUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI goldBalanceText;

        [Tooltip("Manually-placed shop card slots. The number of slots is the shop size.")]
        [SerializeField] private InRunShopUpgradeCard[] upgradeCards;

        [SerializeField] private Button continueButton;
        [SerializeField] private Button rerollButton;
        [SerializeField] private TextMeshProUGUI rerollCostText;

        [SerializeField] private float fadeDuration = 0.3f;

        private Action onContinue;
        private Func<UpgradeDefinition[]> onRerollRequested;
        private int rerollCost;
        private RuntimeStats runtimeStats;
        private RunState runState;

        /// <summary>Number of card slots — useful for the caller to know how many upgrades to pick.</summary>
        public int CardSlotCount => upgradeCards != null ? upgradeCards.Length : 0;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            // Hide without deactivating so listeners stay wired.
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);

            if (rerollButton != null)
                rerollButton.onClick.AddListener(OnRerollClicked);
        }

        /// <summary>
        /// Displays the shop with the given upgrade options.
        /// </summary>
        public void Show(UpgradeDefinition[] options, RuntimeStats stats, RunState state,
                         int rerollCost, Func<UpgradeDefinition[]> rerollGenerator, Action onContinueClicked)
        {
            this.runtimeStats = stats;
            this.runState = state;
            this.rerollCost = rerollCost;
            this.onRerollRequested = rerollGenerator;
            this.onContinue = onContinueClicked;

            PopulateCards(options);
            UpdateRerollButton();
            RefreshGoldDisplay();

            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            StartCoroutine(FadeOut(null));
        }

        private void PopulateCards(UpgradeDefinition[] options)
        {
            if (upgradeCards == null) return;

            for (int i = 0; i < upgradeCards.Length; i++)
            {
                if (upgradeCards[i] == null) continue;

                if (options != null && i < options.Length && options[i] != null)
                    upgradeCards[i].SetUpgrade(options[i], this);
                else
                    upgradeCards[i].Clear();
            }
        }

        public bool TryPurchase(UpgradeDefinition upgrade)
        {
            if (upgrade == null || runState == null || runtimeStats == null) return false;
            if (runState.gold < upgrade.ShopCost) return false;

            runState.gold -= upgrade.ShopCost;
            upgrade.Apply(runtimeStats);
            runState.appliedUpgrades.Add(upgrade);

            RefreshGoldDisplay();
            UpdateRerollButton();

            // Buying one upgrade may make others unaffordable — refresh all cards.
            foreach (var card in upgradeCards)
            {
                if (card != null) card.RefreshAffordability();
            }
            return true;
        }

        public int CurrentGold => runState != null ? runState.gold : 0;

        private void RefreshGoldDisplay()
        {
            if (goldBalanceText != null)
                goldBalanceText.text = $"Gold: {(runState != null ? runState.gold : 0)}";
        }

        private void UpdateRerollButton()
        {
            bool enabled = runtimeStats != null && runtimeStats.ShopRerollEnabled && onRerollRequested != null;

            if (rerollButton != null)
            {
                rerollButton.gameObject.SetActive(enabled);
                rerollButton.interactable = enabled && runState != null && runState.gold >= rerollCost;
            }

            if (rerollCostText != null)
                rerollCostText.text = $"Reroll: {rerollCost}";
        }

        private void OnRerollClicked()
        {
            if (runState == null || onRerollRequested == null) return;
            if (runState.gold < rerollCost) return;

            runState.gold -= rerollCost;
            var newOptions = onRerollRequested.Invoke();
            PopulateCards(newOptions);
            RefreshGoldDisplay();
            UpdateRerollButton();
        }

        private void OnContinueClicked()
        {
            StartCoroutine(FadeOut(() => onContinue?.Invoke()));
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
            if (continueButton != null)
                continueButton.onClick.RemoveAllListeners();
            if (rerollButton != null)
                rerollButton.onClick.RemoveAllListeners();
        }
    }
}
