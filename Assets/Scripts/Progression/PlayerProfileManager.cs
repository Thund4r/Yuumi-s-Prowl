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

        private void Update()
        {
#if UNITY_EDITOR
            // Debug: Press Shift+E to add 100 essence
            if (Input.GetKeyDown(KeyCode.E) && Input.GetKey(KeyCode.LeftShift))
            {
                GrantEssence(100);
                Debug.Log("DEBUG: Added 100 essence");
            }

            // Debug: Press Shift+R to reset all upgrades
            if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftShift))
            {
                DebugResetUpgrades();
            }

            // Debug: Press Shift+M to set essence to 1000
            if (Input.GetKeyDown(KeyCode.M) && Input.GetKey(KeyCode.LeftShift))
            {
                if (Profile != null)
                {
                    Profile.essenceTotal = 1000;
                    SaveProfile();
                    Debug.Log("DEBUG: Set essence to 1000");
                }
            }
#endif
        }

        /// <summary>
        /// Loads the player profile from disk, or creates a new one if it doesn't exist.
        /// Handles missing, empty, and corrupt save files gracefully — a bad file is
        /// backed up (so it isn't silently lost) and a fresh profile is created.
        /// </summary>
        private void LoadProfile()
        {
            if (!File.Exists(saveFilePath))
            {
                Profile = new PlayerProfile();
                Debug.Log("PlayerProfileManager: no save file found. Created new profile.");
                return;
            }

            string json = null;
            try
            {
                json = File.ReadAllText(saveFilePath);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"PlayerProfileManager: could not read save file — {e.Message}. Creating new profile.");
                Profile = new PlayerProfile();
                return;
            }

            // Empty / whitespace-only file — treat as a fresh profile.
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("PlayerProfileManager: save file is empty. Creating new profile.");
                BackupCorruptSave(json);
                Profile = new PlayerProfile();
                return;
            }

            // Parse — JsonUtility throws on malformed JSON and returns null on some inputs.
            PlayerProfile parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<PlayerProfile>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"PlayerProfileManager: save file is corrupt — {e.Message}. Backing it up and creating new profile.");
            }

            if (parsed == null)
            {
                BackupCorruptSave(json);
                Profile = new PlayerProfile();
                return;
            }

            Profile = parsed;
            SanitizeProfile(Profile);
            Debug.Log($"PlayerProfileManager: loaded profile from {saveFilePath}");
        }

        /// <summary>
        /// Repairs a loaded profile so downstream code never hits a null array/field —
        /// e.g. an older save written before metaUpgrades existed.
        /// </summary>
        private static void SanitizeProfile(PlayerProfile profile)
        {
            if (profile.metaUpgrades == null)
                profile.metaUpgrades = new MetaUpgradeState[0];

            if (profile.essenceTotal < 0) profile.essenceTotal = 0;
            if (profile.essenceSpent < 0) profile.essenceSpent = 0;
        }

        /// <summary>
        /// Renames a bad save file to *.corrupt so the player's data isn't silently
        /// overwritten and the file can be inspected later.
        /// </summary>
        private void BackupCorruptSave(string contents)
        {
            try
            {
                string backupPath = saveFilePath + ".corrupt";
                File.WriteAllText(backupPath, contents ?? string.Empty);
                Debug.LogWarning($"PlayerProfileManager: backed up bad save to {backupPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"PlayerProfileManager: failed to back up corrupt save — {e.Message}");
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

#if UNITY_EDITOR
        private void DebugResetUpgrades()
        {
            if (Profile == null)
                return;

            Profile.metaUpgrades = new MetaUpgradeState[0];
            SaveProfile();
            Debug.Log("DEBUG: Reset all upgrades to unpurchased state");
        }
#endif
    }
}
