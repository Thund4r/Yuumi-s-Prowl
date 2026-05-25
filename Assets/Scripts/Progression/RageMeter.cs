using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Purple colour synergy: a meter that fills as the player destroys **purple** balls.
    /// Once full, it activates for a duration, granting the player fire-and-forget homing
    /// for the active window. After the duration expires, the meter resets and must refill.
    /// Non-purple matches do not contribute — purple synergy self-reinforces.
    ///
    /// Gating by RuntimeStats.RageEnabled (set by the RageUnlock anchor upgrade) and the
    /// purple synergy count (RuntimeStats.ColorSynergyCounts[Purple]):
    ///   - RageEnabled = false         → rage meter never activates.
    ///   - RageEnabled = true          → rage activates; rage window grants **strict** homing.
    ///   - rageLoosePurpleCount+       → rage window grants **loose** homing + multi-fire.
    ///
    /// Setup: add to a GameObject in the Game scene; wire MatchProcessor, RuntimeStats,
    /// RunConfig. Optional: a RageMeterUI listens to OnRageChanged / OnRageActivated /
    /// OnRageExpired to drive a bar.
    /// </summary>
    public class RageMeter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchProcessor matchProcessor;
        [SerializeField] private RuntimeStats runtimeStats;
        [SerializeField] private RunConfig config;

        public float CurrentRage { get; private set; }
        public float MaxRage => config != null ? config.rageMeterMax : 100f;
        public bool IsActive { get; private set; }
        public float ActiveTimeRemaining { get; private set; }
        public bool IsUnlocked => runtimeStats != null && runtimeStats.RageEnabled;

        /// <summary>Current rage as a 0–1 fraction, useful for a UI bar.</summary>
        public float Normalized => MaxRage > 0f ? Mathf.Clamp01(CurrentRage / MaxRage) : 0f;

        public System.Action OnRageActivated;
        public System.Action OnRageExpired;
        /// <summary>Fires (current, max) whenever the rage value changes.</summary>
        public System.Action<float, float> OnRageChanged;

        private void OnEnable()
        {
            if (matchProcessor != null)
                matchProcessor.OnBallsDestroyed += HandleBallsDestroyed;
        }

        private void OnDisable()
        {
            if (matchProcessor != null)
                matchProcessor.OnBallsDestroyed -= HandleBallsDestroyed;
        }

        private void Update()
        {
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
            if (count <= 0) return;
            // Purple synergy self-reinforces: only purple matches build the meter.
            if (color != BallColor.Purple) return;

            float perBall = (config != null ? config.rageGainPerBall : 5f)
                            + (runtimeStats != null ? runtimeStats.RageBuildupBonus : 0f);
            CurrentRage = Mathf.Min(MaxRage, CurrentRage + count * Mathf.Max(0f, perBall));
            OnRageChanged?.Invoke(CurrentRage, MaxRage);

            if (CurrentRage >= MaxRage)
                ActivateRage();
        }

        private void ActivateRage()
        {
            IsActive = true;
            float baseDuration = config != null ? config.rageDuration : 5f;
            float bonus = runtimeStats != null ? runtimeStats.RageDurationBonus : 0f;
            ActiveTimeRemaining = Mathf.Max(0.1f, baseDuration + bonus);

            ApplyHomingFlags(true);
            OnRageActivated?.Invoke();
            Debug.Log($"RageMeter: activated for {ActiveTimeRemaining:F1}s (purple count {GetPurpleCount()}).");
        }

        private void DeactivateRage()
        {
            IsActive = false;
            ActiveTimeRemaining = 0f;
            CurrentRage = 0f;

            ApplyHomingFlags(false);
            OnRageChanged?.Invoke(0f, MaxRage);
            OnRageExpired?.Invoke();
        }

        /// <summary>
        /// Flips RuntimeStats homing flags on/off. While rage is active, the loose tier is
        /// granted at high purple count; otherwise the strict tier; off entirely outside rage.
        /// </summary>
        private void ApplyHomingFlags(bool on)
        {
            if (runtimeStats == null) return;
            if (!on)
            {
                runtimeStats.HomingStrictEnabled = false;
                runtimeStats.HomingLooseEnabled = false;
                return;
            }

            int purpleCount = GetPurpleCount();
            int looseThreshold = config != null ? config.rageLoosePurpleCount : 3;
            if (purpleCount >= looseThreshold)
            {
                // Loose subsumes strict; multi-fire kicks in via HomingLooseEnabled.
                runtimeStats.HomingStrictEnabled = true;
                runtimeStats.HomingLooseEnabled = true;
            }
            else
            {
                runtimeStats.HomingStrictEnabled = true;
                runtimeStats.HomingLooseEnabled = false;
            }
        }

        private int GetPurpleCount()
        {
            return runtimeStats != null ? runtimeStats.GetColorSynergyCount(BallColor.Purple) : 0;
        }
    }
}
