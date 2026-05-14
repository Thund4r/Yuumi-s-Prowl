using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Meta shop UI: displays purchasable upgrades and essence balance.
    /// Shows upgrades as cards with progress bars. Player can buy upgrades with essence.
    /// </summary>
    public class MetaShopUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI essenceBalanceText;
        [SerializeField] private Transform upgradeCardsContainer;
        [SerializeField] private MetaShopUpgradeCard upgradeCardPrefab;
        [SerializeField] private Button closeButton;

        [SerializeField] private MetaProgressionSettings metaProgressionSettings;

        private List<MetaShopUpgradeCard> upgradeCards = new List<MetaShopUpgradeCard>();
        private float fadeDuration = 0.3f;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            Hide();
        }

        private void Start()
        {
            InitializeUpgradeCards();
        }

        private void InitializeUpgradeCards()
        {
            if (metaProgressionSettings == null || metaProgressionSettings.metaUpgrades.Length == 0)
            {
                Debug.LogWarning("MetaShopUI: MetaProgressionSettings not assigned or has no upgrades.");
                return;
            }

            foreach (var cfg in metaProgressionSettings.metaUpgrades)
            {
                var card = Instantiate(upgradeCardPrefab, upgradeCardsContainer);
                card.Initialize(cfg, this);
                upgradeCards.Add(card);
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            RefreshDisplay();
            StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            StartCoroutine(FadeOut());
        }

        private void RefreshDisplay()
        {
            if (PlayerProfileManager.Profile == null)
                return;

            essenceBalanceText.text = $"Essence: {PlayerProfileManager.Profile.essenceTotal}";

            foreach (var card in upgradeCards)
            {
                card.RefreshDisplay();
            }
        }

        public void OnUpgradePurchased()
        {
            RefreshDisplay();
        }

        public MetaProgressionSettings GetMetaProgressionSettings()
        {
            return metaProgressionSettings;
        }

        private System.Collections.IEnumerator FadeIn()
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

        private System.Collections.IEnumerator FadeOut()
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
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
        }
    }
}
