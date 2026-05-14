using UnityEngine;
using YuumisProwl;
using YuumisProwl.BallChain;
using YuumisProwl.Progression;

namespace YuumisProwl.PowerUps
{
    /// <summary>
    /// Controls when and where Hammer power-up balls are inserted into the chain.
    ///
    /// Two trigger paths:
    ///   Natural — rolls a spawn chance on a timer during gameplay.
    ///   Reward  — spawns at the cascade location when a match sequence reaches the cascade threshold.
    ///
    /// Only one Hammer ball can exist in the chain at a time.
    ///
    /// All tuning values live in a shared PowerUpSettings asset so they stay
    /// consistent across levels. Assign the same asset in every scene.
    /// </summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private BallSpawner ballSpawner;

        [Header("Settings")]
        [SerializeField] private PowerUpSettings settings;
        [Tooltip("Per-run mutable stats. When assigned, overrides hammer recoil distance.")]
        [SerializeField] private RuntimeStats runtimeStats;

        private float nextRollTime;

        private void Start()
        {
            if (ballChainManager == null)
                Debug.LogError("PowerUpSpawner: BallChainManager not assigned!");
            if (matchProcessor == null)
                Debug.LogError("PowerUpSpawner: MatchProcessor not assigned!");
            if (settings == null)
                Debug.LogError("PowerUpSpawner: PowerUpSettings not assigned!");

            if (matchProcessor != null)
                matchProcessor.OnMatchSequenceComplete += HandleCascadeSequence;

            nextRollTime = Time.time + (settings != null ? settings.naturalSpawnInterval : 20f);
        }

        private void OnDestroy()
        {
            if (matchProcessor != null)
                matchProcessor.OnMatchSequenceComplete -= HandleCascadeSequence;
        }

        private void Update()
        {
            // Don't spawn during the intro animation
            if (ballSpawner != null && ballSpawner.IsPlayingIntro) return;

            if (settings == null) return;

            if (Time.time >= nextRollTime)
            {
                nextRollTime = Time.time + settings.naturalSpawnInterval;

                if (Random.value < settings.naturalSpawnChance)
                {
                    TrySpawnHammer();
                    Debug.Log("PowerUpSpawner: Natural hammer spawn triggered.");
                }
            }
        }

        /// <summary>
        /// Called by MatchProcessor after a full match sequence completes.
        /// Rewards a cascade streak with a Hammer spawn at the cascade location.
        /// </summary>
        private void HandleCascadeSequence(int cascadeCount, int lastGapIndex)
        {
            if (settings != null && cascadeCount >= settings.rewardCascadeThreshold)
            {
                TrySpawnHammer(lastGapIndex);
                Debug.Log($"PowerUpSpawner: Reward hammer spawn at index {lastGapIndex} — {cascadeCount} cascade(s).");
            }
        }

        /// <summary>
        /// Inserts a Hammer ball into the chain.
        /// If spawnAtIndex is provided and valid, spawns there (cascade reward).
        /// Otherwise picks a random spot in the middle third (natural spawn).
        /// Does nothing if a Hammer is already present or the chain is too short.
        /// </summary>
        public void TrySpawnHammer(int spawnAtIndex = -1)
        {
            if (ballChainManager == null) return;

            var chain = ballChainManager.GetBallChain();
            if (chain.Count < 3) return;

            // Only one hammer at a time
            foreach (var node in chain)
            {
                if (node.ball != null && node.ball.PowerUpType == BallPowerUpType.Hammer)
                    return;
            }

            int insertAfterIndex;
            if (spawnAtIndex >= 0 && spawnAtIndex < chain.Count)
            {
                insertAfterIndex = spawnAtIndex;
            }
            else
            {
                // Natural spawn: place in the middle third
                int minIndex = chain.Count / 3;
                int maxIndex = Mathf.Max(minIndex, (2 * chain.Count) / 3 - 1);
                insertAfterIndex = Random.Range(minIndex, maxIndex + 1);
            }

            float recoil = runtimeStats != null ? runtimeStats.HammerRecoilDistance
                         : settings != null ? settings.hammerRecoilDistance
                         : 3f;
            ballChainManager.SpawnHammerBall(insertAfterIndex, recoil);
        }
    }
}
