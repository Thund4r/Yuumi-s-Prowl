using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Single upgrade card in the meta shop. Displays an UpgradeDefinition's rank
    /// progress, current/next bonus, cost, and a buy button.
    /// </summary>
    public class MetaShopUpgradeCard : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI levelText;   // e.g. "Level 2/6"
        [SerializeField] private TextMeshProUGUI bonusText;   // e.g. "×1.20"
        [SerializeField] private Image progressBar;           // fill image
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button buyButton;
        [SerializeField] private CanvasGroup buyButtonCanvasGroup;

        private UpgradeDefinition upgrade;
        private MetaShopUI parentShop;

        public void Initialize(UpgradeDefinition upgradeDef, MetaShopUI shop)
        {
            upgrade = upgradeDef;
            parentShop = shop;

            if (nameText != null) nameText.text = upgrade.UpgradeName;
            if (descriptionText != null) descriptionText.text = upgrade.Description;
            if (iconImage != null && upgrade.Icon != null) iconImage.sprite = upgrade.Icon;

            if (buyButton != null)
                buyButton.onClick.AddListener(OnBuyButtonClicked);

            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (upgrade == null || PlayerProfileManager.Profile == null)
                return;

            int rank = PlayerProfileManager.GetMetaRank(upgrade.UpgradeId); // -1 = none
            int purchases = rank + 1;                                      // ranks owned
            int nextPurchase = purchases;                                  // 0-based index of next
            bool isMaxed = purchases >= upgrade.MaxRank;

            if (levelText != null)
                levelText.text = $"Level {purchases}/{upgrade.MaxRank}";

            if (bonusText != null)
                bonusText.text = purchases > 0 ? upgrade.GetEffectSummary(purchases) : "Not purchased";

            if (progressBar != null)
                progressBar.fillAmount = upgrade.MaxRank > 0 ? purchases / (float)upgrade.MaxRank : 0f;

            if (isMaxed)
            {
                if (costText != null) costText.text = "MAXED";
                if (buyButton != null) buyButton.interactable = false;
                if (buyButtonCanvasGroup != null) buyButtonCanvasGroup.alpha = 0.5f;
            }
            else
            {
                int cost = upgrade.GetEssenceCost(nextPurchase);
                if (costText != null) costText.text = $"Cost: {cost}";
                bool canAfford = PlayerProfileManager.Profile.essenceTotal >= cost;
                if (buyButton != null) buyButton.interactable = canAfford;
                if (buyButtonCanvasGroup != null) buyButtonCanvasGroup.alpha = canAfford ? 1f : 0.6f;
            }
        }

        private void OnBuyButtonClicked()
        {
            if (upgrade == null) return;

            if (PlayerProfileManager.PurchaseUpgrade(upgrade))
                parentShop.OnUpgradePurchased();
        }

        private void OnDestroy()
        {
            if (buyButton != null)
                buyButton.onClick.RemoveAllListeners();
        }
    }
}
