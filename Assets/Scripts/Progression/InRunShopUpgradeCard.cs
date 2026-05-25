using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Single upgrade card in the in-run shop. Card slots are placed manually in the
    /// scene/prefab; the shop populates them with data via SetUpgrade when it opens.
    /// The entire card is the buy button — clicking it purchases the upgrade.
    /// One-time purchase per shop visit.
    /// </summary>
    public class InRunShopUpgradeCard : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;

        [Tooltip("Button component on the card itself — the whole card is clickable to buy.")]
        [SerializeField] private Button cardButton;

        [Tooltip("Optional CanvasGroup on the card, dimmed when unaffordable or sold.")]
        [SerializeField] private CanvasGroup cardCanvasGroup;

        [Tooltip("Optional overlay GameObject shown once the upgrade has been purchased.")]
        [SerializeField] private GameObject soldOverlay;

        [Tooltip("Content root toggled on/off when this slot is unused. Defaults to this GameObject.")]
        [SerializeField] private GameObject content;

        [Tooltip("Optional — card background Image, tinted for colour-synergy upgrades.")]
        [SerializeField] private Image backgroundImage;

        private UpgradeDefinition upgrade;
        private InRunShopUI parentShop;
        private bool purchased;
        private Color defaultBgColor;
        private bool capturedBgColor;

        private void Awake()
        {
            if (content == null) content = gameObject;
            if (backgroundImage != null)
            {
                defaultBgColor = backgroundImage.color;
                capturedBgColor = true;
            }
            if (cardButton != null)
                cardButton.onClick.AddListener(OnCardClicked);
        }

        /// <summary>
        /// Populates this card with an upgrade. Called each time the shop opens or rerolls.
        /// </summary>
        public void SetUpgrade(UpgradeDefinition upgradeDef, InRunShopUI shop)
        {
            upgrade = upgradeDef;
            parentShop = shop;
            purchased = false;

            content.SetActive(true);

            if (nameText != null) nameText.text = upgrade.UpgradeName;
            if (descriptionText != null) descriptionText.text = upgrade.Description;
            if (iconImage != null && upgrade.Icon != null) iconImage.sprite = upgrade.Icon;
            if (costText != null) costText.text = $"{upgrade.ShopCost}";
            if (soldOverlay != null) soldOverlay.SetActive(false);

            // Tint the background for colour-synergy upgrades; restore default otherwise.
            if (backgroundImage != null && capturedBgColor)
            {
                backgroundImage.color = upgrade.IsColorSynergy
                    ? BallColorUtils.GetSynergyBackgroundColor(upgrade.TargetColor)
                    : defaultBgColor;
            }

            RefreshAffordability();
        }

        /// <summary>
        /// Empties this card slot (used when there are fewer upgrades than card slots).
        /// </summary>
        public void Clear()
        {
            upgrade = null;
            content.SetActive(false);
        }

        public void RefreshAffordability()
        {
            if (purchased || upgrade == null) return;

            bool canAfford = parentShop != null && parentShop.CurrentGold >= upgrade.ShopCost;
            if (cardButton != null) cardButton.interactable = canAfford;
            if (cardCanvasGroup != null)
                cardCanvasGroup.alpha = canAfford ? 1f : 0.6f;
        }

        private void OnCardClicked()
        {
            if (purchased || upgrade == null || parentShop == null) return;

            if (parentShop.TryPurchase(upgrade))
            {
                purchased = true;
                if (cardButton != null) cardButton.interactable = false;
                if (cardCanvasGroup != null) cardCanvasGroup.alpha = 0.5f;
                if (soldOverlay != null) soldOverlay.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (cardButton != null)
                cardButton.onClick.RemoveAllListeners();
        }
    }
}
