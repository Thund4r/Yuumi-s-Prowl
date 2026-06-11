using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YuumisProwl.PowerUps;
using YuumisProwl.PowerUps.UI;

namespace YuumisProwl.Progression
{
    /// <summary>One potion the shop is offering: which potion, and its gold cost.</summary>
    public struct PotionOffer
    {
        public PowerUpType type;
        public int cost;
        public PotionOffer(PowerUpType type, int cost) { this.type = type; this.cost = cost; }
    }

    /// <summary>
    /// Single potion card in the in-run shop. Card slots are placed manually in the scene and
    /// populated via SetPotion when the shop opens. The whole card is the buy button.
    /// </summary>
    public class PotionShopCard : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [Tooltip("Button on the card itself — the whole card is clickable to buy.")]
        [SerializeField] private Button cardButton;
        [Tooltip("Optional CanvasGroup, dimmed when unaffordable or sold.")]
        [SerializeField] private CanvasGroup cardCanvasGroup;
        [Tooltip("Optional overlay shown once bought.")]
        [SerializeField] private GameObject soldOverlay;
        [Tooltip("Content root toggled off when this slot is unused. Defaults to this GameObject.")]
        [SerializeField] private GameObject content;

        private PotionOffer offer;
        private InRunShopUI parentShop;
        private bool purchased;
        private bool hasOffer;

        private void Awake()
        {
            if (content == null) content = gameObject;
            if (cardButton != null) cardButton.onClick.AddListener(OnCardClicked);
        }

        public void SetPotion(PotionOffer potionOffer, PowerUpIconDatabase icons, InRunShopUI shop)
        {
            offer = potionOffer;
            parentShop = shop;
            purchased = false;
            hasOffer = true;
            content.SetActive(true);

            if (nameText != null) nameText.text = potionOffer.type.ToString();
            if (costText != null) costText.text = $"{potionOffer.cost}";
            if (iconImage != null && icons != null)
            {
                iconImage.sprite = icons.GetIcon(potionOffer.type);
                iconImage.color = icons.GetTint(potionOffer.type);
            }
            if (soldOverlay != null) soldOverlay.SetActive(false);

            RefreshAffordability();
        }

        public void Clear()
        {
            hasOffer = false;
            content.SetActive(false);
        }

        public void RefreshAffordability()
        {
            if (purchased || !hasOffer) return;
            bool canAfford = parentShop != null && parentShop.CurrentGold >= offer.cost;
            if (cardButton != null) cardButton.interactable = canAfford;
            if (cardCanvasGroup != null) cardCanvasGroup.alpha = canAfford ? 1f : 0.6f;
        }

        private void OnCardClicked()
        {
            if (purchased || !hasOffer || parentShop == null) return;
            if (parentShop.TryPurchasePotion(offer.type, offer.cost))
            {
                purchased = true;
                if (cardButton != null) cardButton.interactable = false;
                if (cardCanvasGroup != null) cardCanvasGroup.alpha = 0.5f;
                if (soldOverlay != null) soldOverlay.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (cardButton != null) cardButton.onClick.RemoveAllListeners();
        }
    }
}
