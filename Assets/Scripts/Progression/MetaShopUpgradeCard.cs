using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Single upgrade card in the meta shop. Displays progress bar, cost, and buy button.
    /// </summary>
    public class MetaShopUpgradeCard : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI levelText; // e.g. "Level 1/6"
        [SerializeField] private Image progressBar;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button buyButton;
        [SerializeField] private CanvasGroup buyButtonCanvasGroup;

        private MetaUpgradeConfig upgradeConfig;
        private MetaProgressionSettings metaProgressionSettings;
        private MetaShopUI parentShop;

        public void Initialize(MetaUpgradeConfig config, MetaShopUI shop)
        {
            upgradeConfig = config;
            parentShop = shop;
            metaProgressionSettings = shop.GetComponent<MetaShopUI>().GetMetaProgressionSettings();

            nameText.text = config.upgradeName;
            descriptionText.text = config.description;

            if (config.icon != null)
                iconImage.sprite = config.icon;

            if (buyButton != null)
                buyButton.onClick.AddListener(OnBuyButtonClicked);

            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (upgradeConfig == null || metaProgressionSettings == null)
                return;

            var profile = PlayerProfileManager.Profile;
            if (profile == null)
                return;

            var upgrade = PlayerProfileManager.GetOrCreateMetaUpgrade(upgradeConfig.upgradeId);
            int currentRank = upgrade.rank;
            int nextRank = currentRank + 1;
            bool isMaxed = nextRank >= upgradeConfig.maxRanks;

            // Update level display
            levelText.text = $"Level {currentRank + 1}/{upgradeConfig.maxRanks}";

            // Update progress bar
            float progress = (currentRank + 1) / (float)upgradeConfig.maxRanks;
            progressBar.fillAmount = progress;

            // Update cost and button state
            if (isMaxed)
            {
                costText.text = "MAXED";
                buyButton.interactable = false;
                if (buyButtonCanvasGroup != null)
                    buyButtonCanvasGroup.alpha = 0.5f;
            }
            else
            {
                costText.text = $"Cost: {upgradeConfig.essenceCostPerRank}";
                bool canAfford = profile.essenceTotal >= upgradeConfig.essenceCostPerRank;
                buyButton.interactable = canAfford;
                if (buyButtonCanvasGroup != null)
                    buyButtonCanvasGroup.alpha = canAfford ? 1f : 0.6f;
            }
        }

        private void OnBuyButtonClicked()
        {
            if (upgradeConfig == null)
                return;

            if (PlayerProfileManager.PurchaseUpgrade(upgradeConfig.upgradeId, metaProgressionSettings))
            {
                parentShop.OnUpgradePurchased();
            }
        }

        private void OnDestroy()
        {
            if (buyButton != null)
                buyButton.onClick.RemoveAllListeners();
        }
    }
}
