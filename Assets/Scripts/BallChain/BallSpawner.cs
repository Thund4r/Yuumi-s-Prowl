using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YuumisProwl;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Spawns ball waves (the opening intro wave plus mid-level waves) and drives the
    /// emergence speed surge.
    /// </summary>
    public class BallSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [Tooltip("Per-run stats. When assigned, ColorWeights bias which colors spawn.")]
        [SerializeField] private YuumisProwl.Progression.RuntimeStats runtimeStats;

        [Header("Spawn Settings")]
        [Tooltip("Number of balls in the opening intro wave.")]
        [SerializeField] private int ballCount = 30;
        [SerializeField] private int colorCount = 4;

        [Header("Intro Wave")]
        [Tooltip("Duration of the opening wave's surge. Longer than a normal wave for a dramatic entrance.")]
        [SerializeField] private float introDuration = 7f;
        [Tooltip("Peak chain speed multiplier for the opening wave's surge.")]
        [SerializeField] private float introSurgeMultiplier = 1.5f;

        [Header("Wave Settings")]
        [Tooltip("Balls dumped into the queue (below the hole) per wave. Normal chain movement pulls them up.")]
        [SerializeField] private int waveBallCount = 20;
        [Tooltip("Chain speed multiplier while a wave surges up out of the hole, then restored to 1.")]
        [SerializeField] private float waveSurgeMultiplier = 2.5f;
        [Tooltip("Seconds the surge multiplier is held before restoring normal speed.")]
        [SerializeField] private float waveSurgeDuration = 1.5f;

        private List<BallColor> recentColors = new List<BallColor>(2);
        private Coroutine surgeRoutine;

        // Expose the configured color count so other systems can read the canonical value
        public int ColorCount => colorCount;

        /// <summary>
        /// True while the intro animation is playing. 
        /// Other systems (e.g. ProjectileSpawner) should check this to block input.
        /// </summary>
        public bool IsPlayingIntro { get; private set; }

        /// <summary>
        /// Fired when the intro finishes and gameplay can begin.
        /// </summary>
        public System.Action OnIntroComplete;

        private void Start()
        {
            if (ballChainManager == null)
            {
                Debug.LogError("BallSpawner: BallChainManager not assigned!");
                enabled = false;
            }
            // StartLevel is driven by LevelManager once a map prefab has been
            // instantiated and its PathController has been bound.
        }

        /// <summary>
        /// Resets spawner state and starts the opening wave.
        /// Called on first Start and by LevelManager on level transitions.
        /// </summary>
        public void StartLevel()
        {
            StopAllCoroutines();
            surgeRoutine = null;
            recentColors.Clear();
            IsPlayingIntro = false;
            StartCoroutine(IntroRoutine());
        }

        /// <summary>
        /// Opening wave. Uses the same queue-and-surge mechanism as mid-level waves, so the
        /// chain eases from the surge peak straight into normal gameplay speed — no stop or
        /// teleport. Blocks input only until the first ball crests the hole, then fires
        /// OnIntroComplete so GameManager's win/wave loop can begin.
        /// </summary>
        private IEnumerator IntroRoutine()
        {
            IsPlayingIntro = true;
            StartWave(ballCount, introSurgeMultiplier, introDuration);

            // Wait until the first ball is on screen (safety timeout in case the path is
            // misconfigured and nothing ever rises out of the hole).
            float elapsed = 0f;
            while (elapsed < introDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            IsPlayingIntro = false;
            OnIntroComplete?.Invoke();
            Debug.Log("Intro wave up — gameplay started.");
        }

        /// <summary>
        /// Spawns the next wave: drops a batch of balls into the queue below the hole and lets
        /// normal chain movement pull them up, surging the chain speed then easing back to
        /// normal. Does NOT block input — nothing hard-writes pathProgress, so the player can
        /// shoot into the wave the instant it crests the hole.
        /// </summary>
        public void SpawnNextWave()
        {
            StartWave(waveBallCount, waveSurgeMultiplier, waveSurgeDuration);
        }

        private void StartWave(int count, float surgeMultiplier, float surgeDuration)
        {
            if (ballChainManager == null) return;

            // Spawn the wave below the hole (SpawnBall appends to the back-most segment at
            // spawnProgress and spaced behind it). The surge lifts it into view from below.
            for (int i = 0; i < count; i++)
                ballChainManager.SpawnBall(PickColor());

            ballChainManager.SetMoving(true);

            if (surgeRoutine != null) StopCoroutine(surgeRoutine);
            surgeRoutine = StartCoroutine(WaveSurge(surgeMultiplier, surgeDuration));
        }

        // Eases the chain speed from the surge peak smoothly down to normal (x1) over the
        // duration. SmoothStep landing means no speed jump when the surge ends — the intro
        // flows straight into gameplay, and mid-level waves settle without a hitch.
        private IEnumerator WaveSurge(float peakMultiplier, float duration)
        {
            float elapsed = 0f;
            float OriSpeed = ballChainManager.GetSpeed();
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ballChainManager.SetSpeed(Mathf.SmoothStep(peakMultiplier, 1, t));
                yield return null;
            }
            ballChainManager.SetSpeed(OriSpeed);
            surgeRoutine = null;
        }

        /// <summary>
        /// Picks the next ball color — weighted by RuntimeStats.ColorWeights when assigned,
        /// otherwise uniform. Both paths avoid 3-in-a-row runs.
        /// </summary>
        private BallColor PickColor()
        {
            if (runtimeStats != null)
                return BallColorUtils.GetWeightedRandomColor(colorCount, recentColors, runtimeStats.ColorWeights);
            return BallColorUtils.GetRandomColor(colorCount, recentColors);
        }

        public void SetColorCount(int count)
        {
            colorCount = Mathf.Clamp(count, 1, 5);
        }
    }
}