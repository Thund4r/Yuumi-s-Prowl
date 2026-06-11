using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YuumisProwl;
using YuumisProwl.Managers;

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
        [Tooltip("Optional. When assigned, injects in-chain disruptor enemies into each wave per the boss's spec.")]
        [SerializeField] private EnemyManager enemyManager;

        [Header("Spawn Settings")]
        [Tooltip("Number of balls in the opening intro wave.")]
        [SerializeField] private int ballCount = 30;
        [SerializeField] private int colorCount = 4;

        [Header("Intro Wave")]
        [Tooltip("Path fraction (0-1) the opening wave's front surges up to before settling to normal speed. Input stays blocked until the lead reaches it.")]
        [Range(0f, 1f)] [SerializeField] private float introSurgeTargetProgress = 0.5f;

        [Header("Wave Settings")]
        [Tooltip("Balls dumped into the queue (below the hole) per wave. Normal chain movement pulls them up.")]
        [SerializeField] private int waveBallCount = 20;
        [Tooltip("Path fraction (0-1) a wave's front surges up to before settling to normal speed.")]
        [Range(0f, 1f)] [SerializeField] private float waveSurgeTargetProgress = 0.3f;

        [Header("Surge")]
        [Tooltip("Chain speed multiplier applied while a wave surges up to its target path fraction.")]
        [Min(1f)] [SerializeField] private float surgeSpeedMultiplier = 2.5f;
        [Tooltip("Fraction of the surge (0-1) over which the multiplier eases back down to 1 at the end, so the landing isn't abrupt. e.g. 0.4 = full speed for the first 60%, then ease over the last 40%.")]
        [Range(0f, 1f)] [SerializeField] private float surgeEaseFraction = 0.4f;
        [Tooltip("Safety cap (seconds) on how long a surge can run if the lead never reaches its target.")]
        [Min(0.5f)] [SerializeField] private float surgeTimeout = 12f;

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
        /// Opening wave. Uses the same snap-to-hole + surge mechanism as mid-level waves, but blocks
        /// input until the surge brings the wave's front up to its target fraction, then fires
        /// OnIntroComplete so GameManager's win/wave loop can begin.
        /// </summary>
        private IEnumerator IntroRoutine()
        {
            IsPlayingIntro = true;
            StartWave(ballCount, introSurgeTargetProgress);

            // Block input until the surge lifts the wave's front to its target (safety-capped).
            float elapsed = 0f;
            while (elapsed < surgeTimeout
                   && ballChainManager.BallCount > 0
                   && ballChainManager.GetLeadProgress() < introSurgeTargetProgress)
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
            StartWave(waveBallCount, waveSurgeTargetProgress);
        }

        private void StartWave(int count, float surgeTargetProgress)
        {
            if (ballChainManager == null) return;

            // Spawn the wave below the hole (SpawnBall appends to the back-most segment at
            // spawnProgress and spaced behind it). The surge lifts it into view from below.
            // The enemy plan (when present) marks which slots spawn as disruptor enemies.
            EnemyType[] plan = enemyManager != null ? enemyManager.BuildWavePlan(count) : null;
            for (int i = 0; i < count; i++)
            {
                EnemyType et = (plan != null) ? plan[i] : EnemyType.None;
                if (et == EnemyType.None)
                    ballChainManager.SpawnBall(PickColor());
                else
                    ballChainManager.SpawnBall(PickColor(), et);
            }

            ballChainManager.SetMoving(true);
            // Skip the dead travel through the spawn-depth: lift the wave to the hole mouth first,
            // then surge from there.
            ballChainManager.SnapChainToHole();

            if (surgeRoutine != null) StopCoroutine(surgeRoutine);
            surgeRoutine = StartCoroutine(WaveSurge(surgeTargetProgress));
        }

        // Boosts the chain speed (via the transient ChainSpeedMultiplier, so the level's base speed
        // is untouched) until the wave's front reaches surgeTargetProgress of the path. The multiplier
        // eases back down to 1 over the last surgeEaseFraction of the climb so the landing isn't abrupt.
        // Position-based, not time-based: the wave always settles at the same point.
        private IEnumerator WaveSurge(float surgeTargetProgress)
        {
            float start = ballChainManager.GetLeadProgress();
            float span = Mathf.Max(0.0001f, surgeTargetProgress - start);

            float elapsed = 0f;
            while (elapsed < surgeTimeout)
            {
                if (ballChainManager.BallCount == 0) break; // nothing to surge
                float lead = ballChainManager.GetLeadProgress();
                if (lead >= surgeTargetProgress) break;     // reached target

                // Full speed for the first (1 - surgeEaseFraction) of the climb, then SmoothStep the
                // multiplier down to 1 over the final stretch.
                float fraction = Mathf.Clamp01((lead - start) / span);
                float easeT = surgeEaseFraction > 0f
                    ? Mathf.InverseLerp(1f - surgeEaseFraction, 1f, fraction)
                    : 0f;
                float mult = Mathf.SmoothStep(surgeSpeedMultiplier, 1f, easeT);
                ballChainManager.SetChainSpeedMultiplier(Mathf.Max(1f, mult));

                elapsed += Time.deltaTime;
                yield return null;
            }

            ballChainManager.SetChainSpeedMultiplier(1f);
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