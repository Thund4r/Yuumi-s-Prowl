using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YuumisProwl.Managers;
using YuumisProwl.BallChain;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Drives the meta-loop of a run: builds the RunNode[] at start, walks it one node
    /// at a time, listens to GameManager win/lose to advance or end the run, and
    /// hands map-loading to LevelManager.
    ///
    /// Setup:
    ///   1. Add a "RunManager" GameObject to the Game scene.
    ///   2. Wire LevelManager, GameManager, RuntimeStats, RunConfig in the inspector.
    ///   3. Make sure LevelManager no longer auto-loads on its own — RunManager.Start
    ///      triggers the first map load.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunConfig config;
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private RuntimeStats runtimeStats;
        [SerializeField] private UpgradeDraftUI upgradeDraftUI;
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private MetaProgressionSettings metaProgressionSettings;

        [Header("Upgrades")]
        [Tooltip("Pool of upgrades that can be drafted in-run.")]
        [SerializeField] private UpgradeDefinition[] upgradePool;

        [Header("Scene Flow")]
        [Tooltip("Scene loaded when a run ends (win or lose).")]
        [SerializeField] private string mainMenuScene = "Main Menu";
        [Tooltip("Seconds to wait after a run ends before returning to the main menu.")]
        [SerializeField] private float pauseBeforeReturn = 1.5f;

        private RunState state;
        public RunState State => state;

        private bool isAwaitingUpgradeSelection = false;

        private void Start()
        {
            if (gameManager == null)  { Debug.LogError("RunManager: GameManager not assigned!"); return; }
            if (levelManager == null) { Debug.LogError("RunManager: LevelManager not assigned!"); return; }
            if (config == null)       { Debug.LogError("RunManager: RunConfig not assigned!"); return; }

            gameManager.OnGameWon  += HandleNodeWon;
            gameManager.OnGameLost += HandleRunLost;

            StartNewRun();
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameWon  -= HandleNodeWon;
                gameManager.OnGameLost -= HandleRunLost;
            }
        }

        /// <summary>
        /// Generates a fresh RunState, resets per-run stats, and loads the first node.
        /// </summary>
        public void StartNewRun()
        {
            state = GenerateRun();
            if (state == null) return;

            if (runtimeStats != null)
            {
                runtimeStats.ResetToDefaults();
                ApplyMetaUpgradesToRunStats();
            }

            Debug.Log($"RunManager: starting new run — {state.nodes.Length} nodes.");
            LoadCurrentNode();
        }

        private void ApplyMetaUpgradesToRunStats()
        {
            if (runtimeStats == null || PlayerProfileManager.Profile == null || metaProgressionSettings == null)
                return;

            var profile = PlayerProfileManager.Profile;
            foreach (var upgrade in profile.metaUpgrades)
            {
                if (upgrade.rank < 0) continue; // Not purchased

                float value = metaProgressionSettings.GetUpgradeValue(upgrade.upgradeId, upgrade.rank);

                switch (upgrade.upgradeId)
                {
                    case "ChargePerBall":
                        runtimeStats.ChargePerBallDestroyed += Mathf.RoundToInt(value);
                        break;
                    case "BallSpeedReduction":
                        // Reduction is stored as additive negative (e.g., -0.1 = 10% reduction)
                        // Will be applied to ballSpeedMult in LoadCurrentNode
                        break;
                }
                // EssenceGain applied during reward calculation
                // DraftReroll tracked separately in UpgradeDraftUI
            }

            Debug.Log($"RunManager: applied {profile.metaUpgrades.Length} meta upgrades to this run.");
        }

        /// <summary>
        /// Gets the player's ball speed reduction from meta upgrades.
        /// Returns a negative value (e.g., -0.1 for 10% reduction).
        /// </summary>
        private float GetBallSpeedReduction()
        {
            if (PlayerProfileManager.Profile == null || metaProgressionSettings == null)
                return 0f;

            foreach (var upgrade in PlayerProfileManager.Profile.metaUpgrades)
            {
                if (upgrade.upgradeId == "BallSpeedReduction" && upgrade.rank >= 0)
                {
                    float reduction = metaProgressionSettings.GetUpgradeValue("BallSpeedReduction", upgrade.rank);
                    return -reduction; // Return as negative for subtraction
                }
            }
            return 0f;
        }

        /// <summary>
        /// Gets the player's available draft rerolls for this run.
        /// </summary>
        public int GetDraftRerollCount()
        {
            if (PlayerProfileManager.Profile == null || metaProgressionSettings == null)
                return 0;

            foreach (var upgrade in PlayerProfileManager.Profile.metaUpgrades)
            {
                if (upgrade.upgradeId == "DraftReroll" && upgrade.rank >= 0)
                {
                    return upgrade.rank + 1; // rank 0 = 1 reroll, rank 1 = 2 rerolls, etc.
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
            RunNode[] nodes = new RunNode[n];

            if (config.allowDuplicates)
            {
                for (int i = 0; i < n; i++)
                {
                    Map pick = config.mapPool[Random.Range(0, config.mapPool.Length)];
                    nodes[i] = new RunNode(RunNodeType.Gameplay, pick);
                }
            }
            else
            {
                // Pick without replacement; refill the bag when exhausted so small pools
                // still produce a full run.
                var bag = new List<Map>(config.mapPool);
                for (int i = 0; i < n; i++)
                {
                    if (bag.Count == 0) bag.AddRange(config.mapPool);
                    int idx = Random.Range(0, bag.Count);
                    nodes[i] = new RunNode(RunNodeType.Gameplay, bag[idx]);
                    bag.RemoveAt(idx);
                }
            }

            return new RunState { nodes = nodes, currentNodeIndex = 0, gold = 0 };
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

                    // Apply ball speed reduction from meta upgrades
                    float speedReduction = GetBallSpeedReduction();
                    ballSpeedMult = Mathf.Max(0.1f, ballSpeedMult + speedReduction); // Clamp to 0.1× minimum

                    Debug.Log($"RunManager: loading floor {state.currentNodeIndex + 1}/{state.nodes.Length} (t={t:F2}) — ballSpeed×{ballSpeedMult:F2}, totalBalls×{totalBallsMult:F2}");
                    levelManager.LoadMap(node.mapPrefab, ballSpeedMult, totalBallsMult);
                    break;

                case RunNodeType.Shop:
                    // Stubbed until step 6 — for now just skip past shop nodes.
                    Debug.Log("RunManager: shop node encountered (not yet implemented). Skipping.");
                    AdvanceToNextNode();
                    break;
            }
        }

        /// <summary>
        /// 0 at the first node, 1 at the last. Used to sample scaling curves.
        /// </summary>
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

            // If it's the last node, don't show draft—just end the run.
            if (state.IsLastNode)
            {
                EndRun(won: true);
                return;
            }

            // Show upgrade draft before advancing to the next node.
            if (upgradeDraftUI != null && upgradePool != null && upgradePool.Length > 0)
            {
                isAwaitingUpgradeSelection = true;
                var options = PickRandomUpgrades(3);
                upgradeDraftUI.Show(options, HandleUpgradeSelected);
            }
            else
            {
                // No upgrade system configured; just advance.
                AdvanceToNextNode();
            }
        }

        private void HandleUpgradeSelected(UpgradeDefinition upgrade)
        {
            isAwaitingUpgradeSelection = false;

            if (upgrade != null && runtimeStats != null)
            {
                upgrade.Apply(runtimeStats);
                if (state != null)
                    state.appliedUpgrades.Add(upgrade);
            }

            AdvanceToNextNode();
        }

        private UpgradeDefinition[] PickRandomUpgrades(int count)
        {
            if (upgradePool == null || upgradePool.Length == 0)
                return new UpgradeDefinition[0];

            count = Mathf.Min(count, upgradePool.Length);
            var picked = new UpgradeDefinition[count];
            var indices = new System.Collections.Generic.List<int>();

            for (int i = 0; i < upgradePool.Length; i++)
                indices.Add(i);

            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, indices.Count);
                picked[i] = upgradePool[indices[idx]];
                indices.RemoveAt(idx);
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

            // Calculate essence reward: base * depth multiplier * difficulty multiplier * meta gain bonus
            int essenceBase = metaProgressionSettings.baseEssencePerFloor * floorsCleared;

            // Depth multiplier: use the average depth (roughly mid-run)
            float avgProgress = floorsCleared > 0 ? (floorsCleared - 1) / (float)Mathf.Max(1, state.nodes.Length - 1) : 0f;
            float depthMult = metaProgressionSettings.SampleEssenceDepthMult(avgProgress);

            // Difficulty multiplier: average of ball speed and total balls curves at the final floor
            float finalProgress = floorsCleared > 0 ? (floorsCleared - 1) / (float)Mathf.Max(1, state.nodes.Length - 1) : 0f;
            float ballSpeedMult = config != null ? config.SampleBallSpeedMult(finalProgress) : 1f;
            float totalBallsMult = config != null ? config.SampleTotalBallsMult(finalProgress) : 1f;
            float difficultyMult = metaProgressionSettings.essenceDifficultyScaling
                ? ballSpeedMult * totalBallsMult
                : 1f;

            // Meta upgrade multiplier (EssenceGain)
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
