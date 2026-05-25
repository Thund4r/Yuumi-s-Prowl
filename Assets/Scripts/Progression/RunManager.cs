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
        [Tooltip("Shared database of every upgrade in the game. Also referenced by MetaShopUI.")]
        [SerializeField] private UpgradeDatabase upgradeDatabase;

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
            {
                matchProcessor.OnMatchSequenceComplete += HandleMatchSequenceComplete;
                matchProcessor.OnBallsDestroyed += HandleBallsDestroyed;
            }

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
            {
                matchProcessor.OnMatchSequenceComplete -= HandleMatchSequenceComplete;
                matchProcessor.OnBallsDestroyed -= HandleBallsDestroyed;
            }
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

            // Explicitly initialize gold every run — 0 by default, or the StartingGold
            // value granted by upgrades. Guarantees gold never carries across runs.
            state.gold = runtimeStats != null ? runtimeStats.StartingGold : 0;

            Debug.Log($"RunManager: starting new run — {state.nodes.Length} nodes.");
            if (runtimeStats != null)
            {
                Debug.Log($"RunManager: RUN-START STATS — Gold={state.gold}, ChargePerBall={runtimeStats.ChargePerBallDestroyed}, " +
                          $"ShopRerollEnabled={runtimeStats.ShopRerollEnabled}, GoldGainMult={runtimeStats.GoldGainMultiplier:F2}, " +
                          $"PierceWidth={runtimeStats.PierceWidthMultiplier:F2}, ExplosionRadius={runtimeStats.ExplosionRadius:F2}");
            }
            LoadCurrentNode();
        }

        /// <summary>
        /// Applies the player's purchased meta upgrades to RuntimeStats at run start.
        /// Each meta upgrade is an UpgradeDefinition in the UpgradeDatabase, matched by
        /// UpgradeId; it is applied once per purchased rank.
        /// </summary>
        private void ApplyMetaUpgradesToRunStats()
        {
            if (runtimeStats == null || PlayerProfileManager.Profile == null)
                return;

            int applied = 0;
            foreach (var saved in PlayerProfileManager.Profile.metaUpgrades)
            {
                if (saved.rank < 0) continue; // not purchased

                UpgradeDefinition def = FindUpgradeById(saved.upgradeId);
                if (def == null)
                {
                    Debug.LogWarning($"RunManager: saved meta upgrade '{saved.upgradeId}' has no matching UpgradeDefinition in the UpgradeDatabase.");
                    continue;
                }

                def.Apply(runtimeStats, saved.rank + 1); // rank+1 = number of purchased ranks
                applied++;
            }

            Debug.Log($"RunManager: applied {applied} meta upgrade(s) to this run.");
        }

        /// <summary>Finds an upgrade in the database by its UpgradeId.</summary>
        private UpgradeDefinition FindUpgradeById(string upgradeId)
        {
            return upgradeDatabase != null ? upgradeDatabase.FindById(upgradeId) : null;
        }

        /// <summary>Number of free draft rerolls for this run — sourced from RuntimeStats.</summary>
        public int GetDraftRerollCount()
        {
            return runtimeStats != null ? runtimeStats.DraftRerollCount : 0;
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
                    // Refresh count-scaled synergy stats — upgrades only change between
                    // floors, so recomputing as each gameplay floor loads is sufficient.
                    RecomputeSynergyStats();

                    float t = GetFloorProgress();
                    float ballSpeedMult = config.SampleBallSpeedMult(t);
                    float totalBallsMult = config.SampleTotalBallsMult(t);

                    // Subtract the player's ball-speed reduction (from meta upgrades).
                    float speedReduction = runtimeStats != null ? runtimeStats.BallSpeedReduction : 0f;
                    ballSpeedMult = Mathf.Max(0.1f, ballSpeedMult - speedReduction);

                    Debug.Log($"RunManager: loading floor {state.currentNodeIndex + 1}/{state.nodes.Length} (t={t:F2}) — ballSpeed×{ballSpeedMult:F2}, totalBalls×{totalBallsMult:F2}");
                    levelManager.LoadMap(node.mapPrefab, ballSpeedMult, totalBallsMult);
                    break;

                case RunNodeType.Shop:
                    OpenShop();
                    break;
            }
        }

        /// <summary>
        /// Recomputes per-run stats that scale with the count of colour-synergy upgrades
        /// owned. Called as each gameplay floor loads — upgrades only change between floors.
        /// </summary>
        private void RecomputeSynergyStats()
        {
            if (runtimeStats == null || config == null || state == null) return;

            // Cache the per-colour synergy counts once for everything that needs them
            // (explosion radius, weights, rage meter, future synergies).
            foreach (BallColor c in System.Enum.GetValues(typeof(BallColor)))
            {
                int count = state.CountColorSynergyUpgrades(c);
                runtimeStats.SetColorSynergyCount(c, count);
                // Colour spawn weight scales with synergy upgrades of that colour owned.
                runtimeStats.SetColorWeight(c, 1f + count * config.colorWeightPerSynergyUpgrade);
            }

            // Explosion radius — count-scaled by red synergy upgrades.
            int redCount = runtimeStats.GetColorSynergyCount(BallColor.Red);
            runtimeStats.ExplosionRadius = config.baseExplosionRadius
                                           + redCount * config.explosionRadiusPerRedUpgrade;
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

            if (upgradeDraftUI != null && upgradeDatabase != null)
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

        /// <summary>
        /// Color-synergy hook: grants bonus gold each time a match of a color the player
        /// has a ColorMatchGold upgrade for is destroyed. Fires once per match.
        /// </summary>
        private void HandleBallsDestroyed(int count, BallColor color)
        {
            if (state == null || runtimeStats == null) return;

            int bonus = runtimeStats.GetColorMatchGold(color);
            if (bonus > 0)
            {
                state.gold += bonus;
                Debug.Log($"RunManager: {color}-match bonus +{bonus} gold. Total: {state.gold}");
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
        /// An upgrade is "available" if it hasn't been acquired up to its MaxRank this run
        /// AND all of its prerequisite upgrades are already owned.
        /// </summary>
        private bool IsAvailable(UpgradeDefinition upgrade)
        {
            if (state == null) return true;
            return state.CanAcquire(upgrade) && upgrade.ArePrerequisitesMet(state);
        }

        private UpgradeDefinition[] PickFilteredUpgrades(int count, System.Func<UpgradeDefinition, bool> predicate)
        {
            if (upgradeDatabase == null || upgradeDatabase.AllUpgrades == null || upgradeDatabase.AllUpgrades.Length == 0)
                return new UpgradeDefinition[0];

            // Collect eligible upgrades.
            var pool = upgradeDatabase.AllUpgrades;
            List<UpgradeDefinition> eligible = new List<UpgradeDefinition>();
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && predicate(pool[i]))
                    eligible.Add(pool[i]);
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

            // Essence-gain bonus from meta upgrades is baked into RuntimeStats at run start.
            float metaGainMult = runtimeStats != null ? runtimeStats.EssenceGainMultiplier : 1f;

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
