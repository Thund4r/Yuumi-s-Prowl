using UnityEngine;
using YuumisProwl;
using YuumisProwl.BallChain;
using YuumisProwl.Progression;

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
        [Tooltip("Per-run mutable stats. When assigned, overrides the matching settings values.")]
        [SerializeField] private RuntimeStats runtimeStats;
        [Tooltip("If true, reaching the charge threshold awards a random potion. Off in the potion rework — potions come from the shop + floor rewards instead.")]
        [SerializeField] private bool awardOnCharge = false;

        private int currentCharge;

        public int CurrentCharge => currentCharge;
        public int ChargeThreshold => runtimeStats != null ? runtimeStats.ChargeThreshold
                                    : settings != null ? settings.chargeThreshold
                                    : 10;

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
            int perBall = runtimeStats != null ? runtimeStats.ChargePerBallDestroyed
                        : settings != null ? settings.chargePerBallDestroyed
                        : 0;
            if (perBall <= 0) return;
            AddCharge(count * perBall);
        }

        private void HandleSequenceComplete(int cascadeCount, int lastGapIndex)
        {
            if (cascadeCount <= 0) return;
            int bonus = runtimeStats != null ? runtimeStats.CascadeBonusCharge
                      : settings != null ? settings.cascadeBonusCharge
                      : 0;
            if (bonus <= 0) return;
            AddCharge(cascadeCount * bonus);
        }

        private void AddCharge(int amount)
        {
            if (amount <= 0) return;

            currentCharge += amount;
            OnChargeChanged?.Invoke(currentCharge, ChargeThreshold);

            if (!awardOnCharge) return; // potion rework: no charge-earned potions

            while (currentCharge >= ChargeThreshold)
            {
                currentCharge -= ChargeThreshold;
                AwardPowerUp();
                OnChargeChanged?.Invoke(currentCharge, ChargeThreshold);
            }
        }

        /// <summary>
        /// Picks a random earnable power-up type and adds it to the inventory.
        /// Pierce is always weight 1.0; Bomb's weight is RuntimeStats.BombAwardWeight
        /// (baseline 1.0 = 50/50). Red synergy upgrades can bias the roll toward Bomb.
        /// </summary>
        private void AwardPowerUp()
        {
            if (inventory == null) return;

            float bombWeight = runtimeStats != null ? Mathf.Max(0f, runtimeStats.BombAwardWeight) : 1f;
            const float pierceWeight = 1f;
            float total = bombWeight + pierceWeight;

            PowerUpType awarded = total > 0f && Random.value * total < bombWeight
                ? PowerUpType.Bomb
                : PowerUpType.Pierce;

            bool added = inventory.AddPowerUp(awarded);
            if (!added)
                Debug.Log("PowerUpChargeTracker: Inventory full — award dropped.");
        }
    }
}
