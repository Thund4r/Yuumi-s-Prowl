using UnityEngine;
using YuumisProwl.BallChain;
using YuumisProwl.Managers;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Purple colour synergy: a meter that fills as the player destroys **purple** balls.
    /// Once the bar is full the meter is **ready** — the player presses the activation key
    /// (or a UI button wired to TryActivateRage) to trigger the rage window. While active,
    /// the player gets fire-and-forget homing for a duration; after the window expires the
    /// meter resets and must refill. Non-purple matches do not contribute — purple synergy
    /// self-reinforces.
    ///
    /// Gating by RuntimeStats.RageEnabled (set by the RageUnlock anchor upgrade):
    ///   - RageEnabled = false → rage meter never activates.
    ///   - RageEnabled = true  → rage activates; the rage window ALWAYS grants **loose**
    ///                           homing (subsumes strict) + multi-fire / rapid fire.
    ///
    /// Round-end refund: if the round ends (win or lose) while rage is active, the meter
    /// stops immediately and returns the *fraction of unused timer* back to CurrentRage —
    /// e.g. ending at 60% time remaining starts the next round at 60% rage. Drops the
    /// "punished for unlucky timing" feel.
    ///
    /// Setup: add to a GameObject in the Game scene; wire MatchProcessor, RuntimeStats,
    /// RunConfig, GameManager. Optional: a RageMeterUI listens to OnRageChanged /
    /// OnRageReady / OnRageActivated / OnRageExpired to drive a bar + activation prompt.
    /// </summary>
    public class RageMeter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private RuntimeStats runtimeStats;
        [SerializeField] private RunConfig config;
        [Tooltip("Used to end rage early on round win/lose and refund unused time. Optional — if null, round-end refund won't fire.")]
        [SerializeField] private GameManager gameManager;

        [Header("Activation")]
        [Tooltip("Keyboard key the player presses to activate rage when the meter is ready. Also exposed as TryActivateRage() so a UI button can drive it on mobile.")]
        [SerializeField] private KeyCode activationKey = KeyCode.Space;

        public float CurrentRage { get; private set; }
        public float MaxRage => config != null ? config.rageMeterMax : 100f;
        public bool IsActive { get; private set; }
        public float ActiveTimeRemaining { get; private set; }
        public bool IsUnlocked => runtimeStats != null && runtimeStats.RageEnabled;

        /// <summary>True when the meter is full and waiting for the player to activate it.</summary>
        public bool IsReady => IsUnlocked && !IsActive && CurrentRage >= MaxRage;

        /// <summary>Key the player presses (keyboard) to activate rage. Exposed so the UI can display it in the prompt.</summary>
        public KeyCode ActivationKey => activationKey;

        /// <summary>Current rage as a 0–1 fraction, useful for a UI bar.</summary>
        public float Normalized => MaxRage > 0f ? Mathf.Clamp01(CurrentRage / MaxRage) : 0f;

        /// <summary>Total active duration captured at activation time — used by the UI to drive the drain animation.</summary>
        public float ActiveDurationTotal { get; private set; }

        public System.Action OnRageActivated;
        public System.Action OnRageExpired;
        /// <summary>Fires when the meter first becomes ready (full but not yet activated).</summary>
        public System.Action OnRageReady;
        /// <summary>Fires (current, max) whenever the rage value changes.</summary>
        public System.Action<float, float> OnRageChanged;

        private void OnEnable()
        {
            if (matchProcessor != null)
                matchProcessor.OnBallsDestroyed += HandleBallsDestroyed;
            if (gameManager != null)
            {
                gameManager.OnGameWon += HandleRoundEnded;
                gameManager.OnGameLost += HandleRoundEnded;
            }
        }

        private void OnDisable()
        {
            if (matchProcessor != null)
                matchProcessor.OnBallsDestroyed -= HandleBallsDestroyed;
            if (gameManager != null)
            {
                gameManager.OnGameWon -= HandleRoundEnded;
                gameManager.OnGameLost -= HandleRoundEnded;
            }
        }

        private void Update()
        {
            // Listen for the activation key while ready. Keyboard input is editor / PC only;
            // mobile builds drive activation via a UI button calling TryActivateRage directly.
            if (IsReady && Input.GetKeyDown(activationKey))
            {
                TryActivateRage();
            }

            if (!IsActive) return;
            ActiveTimeRemaining -= Time.deltaTime;
            if (ActiveTimeRemaining <= 0f)
                DeactivateRage();
        }

        private void HandleBallsDestroyed(int count, BallColor color)
        {
            if (!IsUnlocked) return;
            // Don't fill while active — the player is already in a rage window.
            if (IsActive) return;
            // Don't fill while already ready — wait for the player to consume it.
            if (CurrentRage >= MaxRage) return;
            if (count <= 0) return;
            // Purple synergy self-reinforces: only purple matches build the meter.
            if (color != BallColor.Purple) return;

            float perBall = (config != null ? config.rageGainPerBall : 5f)
                            + (runtimeStats != null ? runtimeStats.RageBuildupBonus : 0f);
            CurrentRage = Mathf.Min(MaxRage, CurrentRage + count * Mathf.Max(0f, perBall));
            OnRageChanged?.Invoke(CurrentRage, MaxRage);

            if (CurrentRage >= MaxRage)
            {
                OnRageReady?.Invoke();
                Debug.Log("RageMeter: ready — awaiting activation input.");
            }
        }

        /// <summary>
        /// Adds rage directly (Orange Conductor charging purple). Respects the same gates as
        /// match-driven fill: no-op while locked, active, or already full.
        /// </summary>
        public void AddRage(float amount)
        {
            if (!IsUnlocked || IsActive || amount <= 0f) return;
            if (CurrentRage >= MaxRage) return;

            CurrentRage = Mathf.Min(MaxRage, CurrentRage + amount);
            OnRageChanged?.Invoke(CurrentRage, MaxRage);

            if (CurrentRage >= MaxRage)
            {
                OnRageReady?.Invoke();
                Debug.Log("RageMeter: ready (arc-charged) — awaiting activation input.");
            }
        }

        /// <summary>
        /// Activates rage if the meter is ready. Safe to call any time — no-op if not ready.
        /// UI buttons should hook their OnClick here.
        /// </summary>
        public void TryActivateRage()
        {
            if (!IsReady) return;
            ActivateRage();
        }

        private void ActivateRage()
        {
            IsActive = true;
            float baseDuration = config != null ? config.rageDuration : 5f;
            float bonus = runtimeStats != null ? runtimeStats.RageDurationBonus : 0f;
            ActiveDurationTotal = Mathf.Max(0.1f, baseDuration + bonus);
            ActiveTimeRemaining = ActiveDurationTotal;

            ApplyHomingFlags(true);
            OnRageActivated?.Invoke();
            Debug.Log($"RageMeter: activated for {ActiveTimeRemaining:F1}s (purple count {GetPurpleCount()}).");
        }

        private void DeactivateRage()
        {
            IsActive = false;
            ActiveTimeRemaining = 0f;
            ActiveDurationTotal = 0f;
            CurrentRage = 0f;

            ApplyHomingFlags(false);
            OnRageChanged?.Invoke(0f, MaxRage);
            OnRageExpired?.Invoke();
        }

        /// <summary>
        /// Called when a round ends (won or lost). If rage is active, stop it and refund
        /// the *unused* portion of the timer back to CurrentRage so the next round doesn't
        /// punish the player for ending mid-rage. If rage is just charging, no-op — the
        /// in-progress meter naturally carries over between rounds.
        /// </summary>
        private void HandleRoundEnded()
        {
            if (!IsActive) return;

            float refundFraction = ActiveDurationTotal > 0f
                ? Mathf.Clamp01(ActiveTimeRemaining / ActiveDurationTotal)
                : 0f;
            float refund = refundFraction * MaxRage;

            IsActive = false;
            ActiveTimeRemaining = 0f;
            ActiveDurationTotal = 0f;
            ApplyHomingFlags(false);
            CurrentRage = Mathf.Min(MaxRage, refund);

            OnRageChanged?.Invoke(CurrentRage, MaxRage);
            OnRageExpired?.Invoke();
            // If the refund happened to top the bar back up, fire ready so the UI prompt
            // re-appears on the next round.
            if (CurrentRage >= MaxRage)
                OnRageReady?.Invoke();

            Debug.Log($"RageMeter: round ended during rage — refunded {refund:F1} ({refundFraction * 100f:F0}% of timer remaining).");
        }

        /// <summary>
        /// Flips RuntimeStats homing flags on/off. Rage always grants loose homing (which
        /// subsumes strict) plus multi-fire / rapid fire; both off outside rage.
        /// </summary>
        private void ApplyHomingFlags(bool on)
        {
            if (runtimeStats == null) return;
            runtimeStats.HomingStrictEnabled = on;
            runtimeStats.HomingLooseEnabled = on;
        }

        private int GetPurpleCount()
        {
            return runtimeStats != null ? runtimeStats.GetColorSynergyCount(BallColor.Purple) : 0;
        }
    }
}
