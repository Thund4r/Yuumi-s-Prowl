using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using YuumisProwl.PowerUps;
using YuumisProwl.PowerUps.UI;

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

        [Tooltip("Manually-placed potion card slots. The number of slots is how many potions the shop offers.")]
        [SerializeField] private PotionShopCard[] potionCards;

        [SerializeField] private Button continueButton;
        [SerializeField] private Button rerollButton;
        [SerializeField] private TextMeshProUGUI rerollCostText;

        [SerializeField] private float fadeDuration = 0.3f;

        private Action onContinue;
        private Func<UpgradeDefinition[]> onRerollRequested;
        private int rerollCost;
        private RuntimeStats runtimeStats;
        private RunState runState;
        private PowerUpInventory inventory;
        private PowerUpIconDatabase potionIcons;

        /// <summary>Number of upgrade card slots — how many upgrades the caller should pick.</summary>
        public int CardSlotCount => upgradeCards != null ? upgradeCards.Length : 0;
        /// <summary>Number of potion card slots — how many potions the caller should offer.</summary>
        public int PotionSlotCount => potionCards != null ? potionCards.Length : 0;

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
        public void Show(UpgradeDefinition[] options, PotionOffer[] potionOffers, RuntimeStats stats, RunState state,
                         PowerUpInventory inventory, PowerUpIconDatabase potionIcons,
                         int rerollCost, Func<UpgradeDefinition[]> rerollGenerator, Action onContinueClicked)
        {
            this.runtimeStats = stats;
            this.runState = state;
            this.inventory = inventory;
            this.potionIcons = potionIcons;
            this.rerollCost = rerollCost;
            this.onRerollRequested = rerollGenerator;
            this.onContinue = onContinueClicked;

            PopulateCards(options);
            PopulatePotions(potionOffers);
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

        private void PopulatePotions(PotionOffer[] offers)
        {
            if (potionCards == null) return;

            for (int i = 0; i < potionCards.Length; i++)
            {
                if (potionCards[i] == null) continue;

                if (offers != null && i < offers.Length)
                    potionCards[i].SetPotion(offers[i], potionIcons, this);
                else
                    potionCards[i].Clear();
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
            RefreshAllAffordability();
            return true;
        }

        /// <summary>Buys a potion: deducts gold and adds it to the inventory. Fails if too poor or the inventory is full.</summary>
        public bool TryPurchasePotion(PowerUpType type, int cost)
        {
            if (runState == null || inventory == null) return false;
            if (runState.gold < cost) return false;
            if (!inventory.AddPowerUp(type)) return false; // inventory full

            runState.gold -= cost;
            RefreshGoldDisplay();
            UpdateRerollButton();
            RefreshAllAffordability();
            return true;
        }

        // A purchase may make other cards unaffordable — refresh both rows.
        private void RefreshAllAffordability()
        {
            if (upgradeCards != null)
                foreach (var card in upgradeCards)
                    if (card != null) card.RefreshAffordability();
            if (potionCards != null)
                foreach (var card in potionCards)
                    if (card != null) card.RefreshAffordability();
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
                rerollCostText.text = $"Reroll: {rerollCost} gold";
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
