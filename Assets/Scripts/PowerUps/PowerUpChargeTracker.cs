using UnityEngine;
using YuumisProwl;
using YuumisProwl.BallChain;

namespace YuumisProwl.PowerUps
{
    /// <summary>
    /// Listens to MatchProcessor events and accumulates "charge" from successful matches.
    /// When the charge threshold is reached, awards a random player-earned power-up to
    /// the PowerUpInventory and resets the meter.
    ///
    /// Setup:
    ///   1. Add to any GameObject in the level scene.
    ///   2. Assign MatchProcessor, PowerUpInventory, and the shared PowerUpSettings asset.
    /// </summary>
    public class PowerUpChargeTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private PowerUpInventory inventory;
        [SerializeField] private PowerUpSettings settings;

        private int currentCharge;

        public int CurrentCharge => currentCharge;
        public int ChargeThreshold => settings != null ? settings.chargeThreshold : 10;

        /// <summary>Fires (currentCharge, threshold) whenever charge changes. For UI meters.</summary>
        public System.Action<int, int> OnChargeChanged;

        private void Start()
        {
            if (matchProcessor == null)
                Debug.LogError("PowerUpChargeTracker: MatchProcessor not assigned!");
            if (inventory == null)
                Debug.LogError("PowerUpChargeTracker: PowerUpInventory not assigned!");
            if (settings == null)
                Debug.LogError("PowerUpChargeTracker: PowerUpSettings not assigned!");

            if (matchProcessor != null)
            {
                matchProcessor.OnBallsDestroyed += HandleBallsDestroyed;
                matchProcessor.OnMatchSequenceComplete += HandleSequenceComplete;
            }
        }

        private void OnDestroy()
        {
            if (matchProcessor != null)
            {
                matchProcessor.OnBallsDestroyed -= HandleBallsDestroyed;
                matchProcessor.OnMatchSequenceComplete -= HandleSequenceComplete;
            }
        }

        private void HandleBallsDestroyed(int count, BallColor color)
        {
            if (settings == null) return;
            AddCharge(count * settings.chargePerBallDestroyed);
        }

        private void HandleSequenceComplete(int cascadeCount, int lastGapIndex)
        {
            if (settings == null) return;
            if (cascadeCount > 0)
                AddCharge(cascadeCount * settings.cascadeBonusCharge);
        }

        private void AddCharge(int amount)
        {
            if (amount <= 0) return;

            currentCharge += amount;
            OnChargeChanged?.Invoke(currentCharge, ChargeThreshold);

            while (currentCharge >= ChargeThreshold)
            {
                currentCharge -= ChargeThreshold;
                AwardPowerUp();
                OnChargeChanged?.Invoke(currentCharge, ChargeThreshold);
            }
        }

        /// <summary>
        /// Picks a random earnable power-up type and adds it to the inventory.
        /// Extend the type list as new power-ups are added.
        /// </summary>
        private void AwardPowerUp()
        {
            if (inventory == null) return;

            PowerUpType[] pool = { PowerUpType.Pierce, PowerUpType.Bomb };
            PowerUpType awarded = pool[Random.Range(0, pool.Length)];

            bool added = inventory.AddPowerUp(awarded);
            if (!added)
                Debug.Log("PowerUpChargeTracker: Inventory full — award dropped.");
        }
    }
}
