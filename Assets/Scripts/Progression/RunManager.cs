using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YuumisProwl.Managers;

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
                runtimeStats.ResetToDefaults();

            Debug.Log($"RunManager: starting new run — {state.nodes.Length} nodes.");
            LoadCurrentNode();
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
                    levelManager.LoadMap(node.mapPrefab);
                    break;

                case RunNodeType.Shop:
                    // Stubbed until step 6 — for now just skip past shop nodes.
                    Debug.Log("RunManager: shop node encountered (not yet implemented). Skipping.");
                    AdvanceToNextNode();
                    break;
            }
        }

        private void HandleNodeWon()
        {
            AdvanceToNextNode();
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
            // TODO step 5: grant essence based on floors cleared, save PlayerProfile.
            StartCoroutine(ReturnToMenuRoutine());
        }

        private IEnumerator ReturnToMenuRoutine()
        {
            yield return new WaitForSeconds(pauseBeforeReturn);
            if (!string.IsNullOrEmpty(mainMenuScene))
                SceneManager.LoadScene(mainMenuScene);
        }
    }
}
