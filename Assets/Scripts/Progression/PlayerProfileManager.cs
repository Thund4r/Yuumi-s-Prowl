using UnityEngine;
using System.IO;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Manages persistent player profile (save/load from JSON).
    /// Add one instance to the main menu scene; it persists via DontDestroyOnLoad.
    /// </summary>
    public class PlayerProfileManager : MonoBehaviour
    {
        private static PlayerProfileManager instance;
        public static PlayerProfile Profile { get; private set; }

        private string saveFilePath;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            saveFilePath = Path.Combine(Application.persistentDataPath, "PlayerProfile.json");
            LoadProfile();
        }

        /// <summary>
        /// Loads the player profile from disk, or creates a new one if it doesn't exist.
        /// </summary>
        private void LoadProfile()
        {
            if (File.Exists(saveFilePath))
            {
                try
                {
                    string json = File.ReadAllText(saveFilePath);
                    Profile = JsonUtility.FromJson<PlayerProfile>(json);
                    Debug.Log($"PlayerProfileManager: loaded profile from {saveFilePath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"PlayerProfileManager: failed to load profile — {e.Message}. Creating new profile.");
                    Profile = new PlayerProfile();
                }
            }
            else
            {
                Profile = new PlayerProfile();
                Debug.Log($"PlayerProfileManager: no save file found. Created new profile.");
            }
        }

        /// <summary>
        /// Saves the current profile to disk.
        /// </summary>
        public static void SaveProfile()
        {
            if (Profile == null)
            {
                Debug.LogWarning("PlayerProfileManager: cannot save — Profile is null.");
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(instance.saveFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(Profile, prettyPrint: true);
                File.WriteAllText(instance.saveFilePath, json);
                Debug.Log($"PlayerProfileManager: saved profile to {instance.saveFilePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"PlayerProfileManager: failed to save profile — {e.Message}");
            }
        }

        /// <summary>
        /// Gets or creates a meta upgrade state by ID.
        /// </summary>
        public static MetaUpgradeState GetOrCreateMetaUpgrade(string upgradeId)
        {
            if (Profile == null)
                return null;

            foreach (var upgrade in Profile.metaUpgrades)
            {
                if (upgrade.upgradeId == upgradeId)
                    return upgrade;
            }

            var newUpgrade = new MetaUpgradeState(upgradeId);
            int newLength = Profile.metaUpgrades.Length + 1;
            System.Array.Resize(ref Profile.metaUpgrades, newLength);
            Profile.metaUpgrades[newLength - 1] = newUpgrade;
            return newUpgrade;
        }

        /// <summary>
        /// Grants essence and saves.
        /// </summary>
        public static void GrantEssence(int amount)
        {
            if (Profile == null)
                return;

            Profile.essenceTotal += amount;
            SaveProfile();
            Debug.Log($"PlayerProfileManager: granted {amount} essence. Total: {Profile.essenceTotal}");
        }

        /// <summary>
        /// Purchases a meta upgrade rank. Returns true if successful, false if insufficient essence or at max rank.
        /// </summary>
        public static bool PurchaseUpgrade(string upgradeId, MetaProgressionSettings settings)
        {
            if (Profile == null || settings == null)
                return false;

            var cfg = settings.GetUpgradeConfig(upgradeId);
            if (cfg == null)
            {
                Debug.LogError($"PlayerProfileManager: upgrade config not found for {upgradeId}");
                return false;
            }

            var upgrade = GetOrCreateMetaUpgrade(upgradeId);
            int nextRank = upgrade.rank + 1;

            if (nextRank >= cfg.maxRanks)
            {
                Debug.LogWarning($"PlayerProfileManager: {upgradeId} already at max rank {cfg.maxRanks}");
                return false;
            }

            if (Profile.essenceTotal < cfg.essenceCostPerRank)
            {
                Debug.LogWarning($"PlayerProfileManager: insufficient essence ({Profile.essenceTotal} < {cfg.essenceCostPerRank})");
                return false;
            }

            Profile.essenceTotal -= cfg.essenceCostPerRank;
            Profile.essenceSpent += cfg.essenceCostPerRank;
            upgrade.rank = nextRank;

            SaveProfile();
            Debug.Log($"PlayerProfileManager: purchased {upgradeId} rank {nextRank + 1}. Essence remaining: {Profile.essenceTotal}");
            return true;
        }
    }
}
