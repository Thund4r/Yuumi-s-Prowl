using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YuumisProwl.Managers;
using YuumisProwl.BallChain;
using YuumisProwl.VFX;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Drives the meta-loop of a run: builds the RunNode[] at start, walks it one node
    /// at a time, listens to GameManager win/lose to advance or end the run, and
    /// hands map-loading to LevelManager.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunConfig config;
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private RuntimeStats runtimeStats;
        [SerializeField] private UpgradeDraftUI upgradeDraftUI;
        [SerializeField] private InRunShopUI inRunShopUI;
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private MetaProgressionSettings metaProgressionSettings;
        [Tooltip("Optional — used to show a floating gold popup after cascades.")]
        [SerializeField] private MatchEffectPlayer matchEffectPlayer;

        [Header("Upgrades")]
        [Tooltip("Pool of upgrades available in this run. Each upgrade's IsDraftable / IsShoppable flags decide which contexts it appears in.")]
        [SerializeField] private UpgradeDefinition[] upgradePool;

        [Header("Scene Flow")]
        [Tooltip("Scene loaded when a run ends (win or lose).")]
        [SerializeField] private string mainMenuScene = "Main Menu";
        [Tooltip("Seconds to wait after a run ends before returning to the main menu.")]
        [SerializeField] private float pauseBeforeReturn = 1.5f;

        private RunState state;
        public RunState State => state;

        private void Start()
        {
            if (gameManager == null)  { Debug.LogError("RunManager: GameManager not assigned!"); return; }
            if (levelManager == null) { Debug.LogError("RunManager: LevelManager not assigned!"); return; }
            if (config == null)       { Debug.LogError("RunManager: RunConfig not assigned!"); return; }

            gameManager.OnGameWon  += HandleNodeWon;
            gameManager.OnGameLost += HandleRunLost;

            if (matchProcessor != null)
                matchProcessor.OnMatchSequenceComplete += HandleMatchSequenceComplete;

            StartNewRun();
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameWon  -= HandleNodeWon;
                gameManager.OnGameLost -= HandleRunLost;
            }
            if (matchProcessor != null)
                matchProcessor.OnMatchSequenceComplete -= HandleMatchSequenceComplete;
        }

        public void StartNewRun()
        {
            state = GenerateRun();
            if (state == null) return;

            if (runtimeStats != null)
            {
                runtimeStats.ResetToDefaults();
                ApplyMetaUpgradesToRunStats();
            }
            else
            {
                Debug.LogWarning("RunManager: runtimeStats not assigned — per-run stats will NOT reset between runs!");
            }

            Debug.Log($"RunManager: starting new run — {state.nodes.Length} nodes.");
            if (runtimeStats != null)
            {
                Debug.Log($"RunManager: RUN-START STATS — ChargePerBall={runtimeStats.ChargePerBallDestroyed}, " +
                          $"ShopRerollEnabled={runtimeStats.ShopRerollEnabled}, GoldGainMult={runtimeStats.GoldGainMultiplier:F2}, " +
                          $"PierceWidth={runtimeStats.PierceWidthMultiplier:F2}, BombRadius={runtimeStats.BombRadius:F2}");
            }
            LoadCurrentNode();
        }

        private void ApplyMetaUpgradesToRunStats()
        {
            if (runtimeStats == null || PlayerProfileManager.Profile == null || metaProgressionSettings == null)
                return;

            var profile = PlayerProfileManager.Profile;
            foreach (var upgrade in profile.metaUpgrades)
            {
                if (upgrade.rank < 0) continue;

                float value = metaProgressionSettings.GetUpgradeValue(upgrade.upgradeId, upgrade.rank);

                switch (upgrade.upgradeId)
                {
                    case "ChargePerBall":
                        runtimeStats.ChargePerBallDestroyed += Mathf.RoundToInt(value);
                        break;
                    case "BallSpeedReduction":
                        // Reduction applied to ballSpeedMult in LoadCurrentNode
                        break;
                }
                // EssenceGain applied during reward calculation
                // DraftReroll tracked separately in UpgradeDraftUI
            }

            Debug.Log($"RunManager: applied {profile.metaUpgrades.Length} meta upgrades to this run.");
        }

        private float GetBallSpeedReduction()
        {
            if (PlayerProfileManager.Profile == null || metaProgressionSettings == null)
                return 0f;

            foreach (var upgrade in PlayerProfileManager.Profile.metaUpgrades)
            {
                if (upgrade.upgradeId == "BallSpeedReduction" && upgrade.rank >= 0)
                {
                    float reduction = metaProgressionSettings.GetUpgradeValue("BallSpeedReduction", upgrade.rank);
                    return -reduction;
                }
            }
            return 0f;
        }

        public int GetDraftRerollCount()
        {
            if (PlayerProfileManager.Profile == null || metaProgressionSettings == null)
                return 0;

            foreach (var upgrade in PlayerProfileManager.Profile.metaUpgrades)
            {
                if (upgrade.upgradeId == "DraftReroll" && upgrade.rank >= 0)
                {
                    return upgrade.rank + 1;
                }
            }
            return 0;
        }

        private RunState GenerateRun()
        {
            if (config.mapPool == null || config.mapPool.Length == 0)
            {
                Debug.LogError("RunManager: RunConfig.mapPool is empty — cannot generate a run.");
                return null;
            }

            int n = Mathf.Max(1, config.mapCount);

            // Build the list of gameplay nodes first.
            List<Map> gameplayMaps = new List<Map>();
            if (config.allowDuplicates)
            {
                for (int i = 0; i < n; i++)
                    gameplayMaps.Add(config.mapPool[Random.Range(0, config.mapPool.Length)]);
            }
            else
            {
                var bag = new List<Map>(config.mapPool);
                for (int i = 0; i < n; i++)
                {
                    if (bag.Count == 0) bag.AddRange(config.mapPool);
                    int idx = Random.Range(0, bag.Count);
                    gameplayMaps.Add(bag[idx]);
                    bag.RemoveAt(idx);
                }
            }

            // Interleave shop nodes after the specified gameplay floor indices.
            List<RunNode> nodeList = new List<RunNode>();
            for (int i = 0; i < gameplayMaps.Count; i++)
            {
                nodeList.Add(new RunNode(RunNodeType.Gameplay, gameplayMaps[i]));
                // After this gameplay floor, insert a shop if configured. Don't insert after
                // the very last gameplay floor — the run is over at that point.
                if (i < gameplayMaps.Count - 1 && config.HasShopAfterFloor(i))
                    nodeList.Add(new RunNode(RunNodeType.Shop, null));
            }

            return new RunState { nodes = nodeList.ToArray(), currentNodeIndex = 0, gold = 0 };
        }

        private void LoadCurrentNode()
        {
            RunNode node = state.CurrentNode;
            if (node == null)
            {
                EndRun(won: true);
                return;
            }

            switch (node.type)
            {
                case RunNodeType.Gameplay:
                    if (node.mapPrefab == null)
                    {
                        Debug.LogError($"RunManager: node {state.currentNodeIndex} has null mapPrefab — skipping.");
                        AdvanceToNextNode();
                        return;
                    }
                    float t = GetFloorProgress();
                    float ballSpeedMult = config.SampleBallSpeedMult(t);
                    float totalBallsMult = config.SampleTotalBallsMult(t);

                    float speedReduction = GetBallSpeedReduction();
                    ballSpeedMult = Mathf.Max(0.1f, ballSpeedMult + speedReduction);

                    Debug.Log($"RunManager: loading floor {state.currentNodeIndex + 1}/{state.nodes.Length} (t={t:F2}) — ballSpeed×{ballSpeedMult:F2}, totalBalls×{totalBallsMult:F2}");
                    levelManager.LoadMap(node.mapPrefab, ballSpeedMult, totalBallsMult);
                    break;

                case RunNodeType.Shop:
                    OpenShop();
                    break;
            }
        }

        private void OpenShop()
        {
            if (inRunShopUI == null)
            {
                Debug.LogWarning("RunManager: Shop node reached but InRunShopUI is not assigned. Skipping.");
                AdvanceToNextNode();
                return;
            }

            // The number of upgrades shown is the number of card slots in the shop UI.
            int slots = inRunShopUI.CardSlotCount;
            var options = PickShopUpgrades(slots);
            if (options.Length == 0)
            {
                Debug.LogWarning("RunManager: No shop-eligible upgrades found in pool. Skipping shop.");
                AdvanceToNextNode();
                return;
            }

            inRunShopUI.Show(
                options,
                runtimeStats,
                state,
                config.shopRerollCost,
                () => PickShopUpgrades(slots),
                AdvanceToNextNode
            );
        }

        private float GetFloorProgress()
        {
            int total = state.nodes.Length;
            if (total <= 1) return 0f;
            return (float)state.currentNodeIndex / (total - 1);
        }

        private void HandleNodeWon()
        {
            if (ballChainManager != null)
            {
                ballChainManager.SetMoving(false);
                ballChainManager.ClearChain();
            }

            // Award gold for clearing this gameplay floor.
            GrantGoldForFloor();

            if (state.IsLastNode)
            {
                EndRun(won: true);
                return;
            }

            if (upgradeDraftUI != null && upgradePool != null && upgradePool.Length > 0)
            {
                var options = PickDraftUpgrades(3);
                if (options.Length == 0)
                {
                    Debug.LogWarning("RunManager: No draft-eligible upgrades available. Advancing.");
                    AdvanceToNextNode();
                    return;
                }
                int rerollCount = GetDraftRerollCount();
                upgradeDraftUI.Show(options, HandleUpgradeSelected, rerollCount, () => PickDraftUpgrades(3));
            }
            else
            {
                AdvanceToNextNode();
            }
        }

        private void GrantGoldForFloor()
        {
            if (config == null || state == null) return;

            // Flat gold per floor — does not scale with depth or difficulty.
            float goldGainMult = runtimeStats != null ? runtimeStats.GoldGainMultiplier : 1f;
            int gold = Mathf.RoundToInt(config.baseGoldPerFloor * goldGainMult);

            state.gold += gold;
            Debug.Log($"RunManager: awarded {gold} gold for clearing floor. Total: {state.gold}");
        }

        private void HandleMatchSequenceComplete(int cascadeCount, int lastGapGlobalIndex)
        {
            if (config == null || state == null || cascadeCount <= 0) return;

            int perCascadeBonus = config.goldPerCascadeBonus;
            if (runtimeStats != null)
                perCascadeBonus += runtimeStats.GoldPerCascade;

            float goldGainMult = runtimeStats != null ? runtimeStats.GoldGainMultiplier : 1f;
            int gold = Mathf.RoundToInt(cascadeCount * perCascadeBonus * goldGainMult);

            if (gold > 0)
            {
                state.gold += gold;
                Debug.Log($"RunManager: cascade bonus +{gold} gold ({cascadeCount} cascades). Total: {state.gold}");

                if (matchEffectPlayer != null)
                    matchEffectPlayer.ShowGoldPopup(gold);
            }
        }

        private void HandleUpgradeSelected(UpgradeDefinition upgrade)
        {
            if (upgrade != null && runtimeStats != null)
            {
                upgrade.Apply(runtimeStats);
                if (state != null)
                    state.appliedUpgrades.Add(upgrade);
            }

            AdvanceToNextNode();
        }

        /// <summary>
        /// Picks N random upgrades that are IsDraftable and not non-stackable-already-owned.
        /// </summary>
        private UpgradeDefinition[] PickDraftUpgrades(int count)
        {
            return PickFilteredUpgrades(count, u => u.IsDraftable && IsAvailable(u));
        }

        /// <summary>
        /// Picks N random upgrades that are IsShoppable and not non-stackable-already-owned.
        /// </summary>
        private UpgradeDefinition[] PickShopUpgrades(int count)
        {
            return PickFilteredUpgrades(count, u => u.IsShoppable && IsAvailable(u));
        }

        /// <summary>
        /// An upgrade is "available" if it's stackable, or if it's not stackable and hasn't been acquired yet.
        /// </summary>
        private bool IsAvailable(UpgradeDefinition upgrade)
        {
            if (upgrade.IsStackable) return true;
            return state == null || !state.HasUpgrade(upgrade);
        }

        private UpgradeDefinition[] PickFilteredUpgrades(int count, System.Func<UpgradeDefinition, bool> predicate)
        {
            if (upgradePool == null || upgradePool.Length == 0)
                return new UpgradeDefinition[0];

            // Collect eligible upgrades.
            List<UpgradeDefinition> eligible = new List<UpgradeDefinition>();
            for (int i = 0; i < upgradePool.Length; i++)
            {
                if (upgradePool[i] != null && predicate(upgradePool[i]))
                    eligible.Add(upgradePool[i]);
            }

            count = Mathf.Min(count, eligible.Count);
            var picked = new UpgradeDefinition[count];
            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, eligible.Count);
                picked[i] = eligible[idx];
                eligible.RemoveAt(idx);
            }

            return picked;
        }

        private void AdvanceToNextNode()
        {
            if (state == null) return;

            if (state.IsLastNode)
            {
                EndRun(won: true);
                return;
            }

            state.currentNodeIndex++;
            LoadCurrentNode();
        }

        private void HandleRunLost()
        {
            EndRun(won: false);
        }

        private void EndRun(bool won)
        {
            int cleared = state != null ? state.FloorsCleared + (won ? 1 : 0) : 0;
            Debug.Log($"=== RUN ENDED ({(won ? "WON" : "LOST")}) — floors cleared: {cleared} ===");

            if (ballChainManager != null)
            {
                ballChainManager.SetMoving(false);
                ballChainManager.ClearChain();
            }

            GrantEssenceReward(cleared, won);
            StartCoroutine(ReturnToMenuRoutine());
        }

        private void GrantEssenceReward(int floorsCleared, bool won)
        {
            if (floorsCleared <= 0 || metaProgressionSettings == null)
                return;

            int essenceBase = metaProgressionSettings.baseEssencePerFloor * floorsCleared;

            float avgProgress = floorsCleared > 0 ? (floorsCleared - 1) / (float)Mathf.Max(1, state.nodes.Length - 1) : 0f;
            float depthMult = metaProgressionSettings.SampleEssenceDepthMult(avgProgress);

            float finalProgress = floorsCleared > 0 ? (floorsCleared - 1) / (float)Mathf.Max(1, state.nodes.Length - 1) : 0f;
            float ballSpeedMult = config != null ? config.SampleBallSpeedMult(finalProgress) : 1f;
            float totalBallsMult = config != null ? config.SampleTotalBallsMult(finalProgress) : 1f;
            float difficultyMult = metaProgressionSettings.essenceDifficultyScaling
                ? ballSpeedMult * totalBallsMult
                : 1f;

            float metaGainMult = 1f;
            if (PlayerProfileManager.Profile != null && metaProgressionSettings != null)
            {
                foreach (var upgrade in PlayerProfileManager.Profile.metaUpgrades)
                {
                    if (upgrade.upgradeId == "EssenceGain" && upgrade.rank >= 0)
                    {
                        metaGainMult = metaProgressionSettings.GetUpgradeValue("EssenceGain", upgrade.rank);
                        break;
                    }
                }
            }

            int essenceGranted = Mathf.Max(1, Mathf.RoundToInt(essenceBase * depthMult * difficultyMult * metaGainMult));
            PlayerProfileManager.GrantEssence(essenceGranted);
            Debug.Log($"RunManager: granted {essenceGranted} essence ({floorsCleared} floors × {depthMult:F2} depth × {difficultyMult:F2} difficulty × {metaGainMult:F2} meta)");
        }

        private IEnumerator ReturnToMenuRoutine()
        {
            yield return new WaitForSeconds(pauseBeforeReturn);
            if (!string.IsNullOrEmpty(mainMenuScene))
                SceneManager.LoadScene(mainMenuScene);
        }
    }
}
