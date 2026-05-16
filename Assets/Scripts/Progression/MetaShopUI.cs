using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Meta shop UI: displays purchasable meta upgrades and the essence balance.
    /// Meta upgrades are ordinary UpgradeDefinition assets with IsMetaShop enabled;
    /// assign them to the metaUpgrades array. A card is instantiated per upgrade.
    /// </summary>
    public class MetaShopUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI essenceBalanceText;
        [SerializeField] private Transform upgradeCardsContainer;
        [SerializeField] private MetaShopUpgradeCard upgradeCardPrefab;
        [SerializeField] private Button closeButton;

        [Tooltip("Shared upgrade database. The meta shop displays every upgrade flagged IsMetaShop.")]
        [SerializeField] private UpgradeDatabase upgradeDatabase;

        [SerializeField] private float fadeDuration = 0.3f;

        private List<MetaShopUpgradeCard> upgradeCards = new List<MetaShopUpgradeCard>();

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            // Hide without deactivating so Start() can run InitializeUpgradeCards.
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
        }

        private void Start()
        {
            InitializeUpgradeCards();
        }

        private void InitializeUpgradeCards()
        {
            if (upgradeDatabase == null)
            {
                Debug.LogWarning("MetaShopUI: UpgradeDatabase not assigned.");
                return;
            }

            var metaUpgrades = upgradeDatabase.GetMetaShopUpgrades();
            if (metaUpgrades.Count == 0)
            {
                Debug.LogWarning("MetaShopUI: the UpgradeDatabase has no upgrades flagged IsMetaShop.");
                return;
            }

            foreach (var upgrade in metaUpgrades)
            {
                var card = Instantiate(upgradeCardPrefab, upgradeCardsContainer);
                card.Initialize(upgrade, this);
                upgradeCards.Add(card);
            }
        }

        public void Show()
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            RefreshDisplay();
            StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            StartCoroutine(FadeOut());
        }

        private void RefreshDisplay()
        {
            if (PlayerProfileManager.Profile != null && essenceBalanceText != null)
                essenceBalanceText.text = $"Essence: {PlayerProfileManager.Profile.essenceTotal}";

            foreach (var card in upgradeCards)
            {
                if (card != null) card.RefreshDisplay();
            }
        }

        /// <summary>Called by a card after a successful purchase — refresh balance and all cards.</summary>
        public void OnUpgradePurchased()
        {
            RefreshDisplay();
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

        private IEnumerator FadeOut()
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
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
        }
    }
}
