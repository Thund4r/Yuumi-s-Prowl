using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YuumisProwl;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Spawns balls and manages the intro animation.
    /// </summary>
    public class BallSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;
        [Tooltip("Per-run stats. When assigned, ColorWeights bias which colors spawn.")]
        [SerializeField] private YuumisProwl.Progression.RuntimeStats runtimeStats;

        [Header("Spawn Settings")]
        [Tooltip("How many balls to show in the intro animation. Remaining balls trickle in as tail spawns.")]
        [SerializeField] private int ballCount = 30;
        [SerializeField] private int colorCount = 4;
        [Tooltip("Total balls for this level. Overridden by LevelManager at runtime.")]
        [SerializeField] private int totalBallsToSpawn = 50;

        [Header("Intro Animation")]
        [SerializeField] private float introDuration = 7f;
        [SerializeField] private AnimationCurve introSpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private List<BallColor> recentColors = new List<BallColor>(2);
        private int ballsSpawned = 0;

        // Expose the configured color count so other systems can read the canonical value
        public int ColorCount => colorCount;
        public bool AllBallsSpawned => ballsSpawned >= totalBallsToSpawn;
        public int BallsRemaining => Mathf.Max(0, totalBallsToSpawn - ballsSpawned);

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
        /// Resets spawner state and starts the intro animation.
        /// Called on first Start and by LevelManager on level transitions.
        /// </summary>
        public void StartLevel()
        {
            StopAllCoroutines();
            ballsSpawned = 0;
            recentColors.Clear();
            IsPlayingIntro = false;
            StartCoroutine(SpawnAndAnimate());
        }

        private IEnumerator SpawnAndAnimate()
        {
            IsPlayingIntro = true;

            // Pause normal chain movement during intro
            ballChainManager.SetMoving(false);

            // Spawn all balls at the start of the path, stacked at progress 0
            SpawnAllBalls();

            // Animate them spreading out along the path
            yield return StartCoroutine(PlayIntroAnimation());

            // Resume normal gameplay
            ballChainManager.SetMoving(true);
            IsPlayingIntro = false;
            OnIntroComplete?.Invoke();

            Debug.Log("Intro complete — gameplay started.");
        }

        /// <summary>
        /// Spawns all balls at progress 0, tightly packed.
        /// They'll be animated to their final positions by the intro.
        /// </summary>
        private void SpawnAllBalls()
        {
            int introCount = Mathf.Min(ballCount, totalBallsToSpawn);
            for (int i = 0; i < introCount; i++)
            {
                ballChainManager.SpawnBall(PickColor());
                ballsSpawned++;
            }

            // Stack all balls at progress 0
            var chain = ballChainManager.GetBallChain();
            for (int i = 0; i < chain.Count; i++)
            {
                chain[i].pathProgress = 0f;
            }
        }

        /// <summary>
        /// Animates balls from stacked at the start to evenly spaced along the path.
        /// Uses an animation curve: accelerates out, then decelerates to a stop.
        /// </summary>
        private IEnumerator PlayIntroAnimation()
        {
            var chain = ballChainManager.GetBallChain();
            if (chain.Count == 0) yield break;

            float pathLength = ballChainManager.GetPathLength();
            if (pathLength <= 0f) yield break;

            float spacingProgress = ballChainManager.BallSpacing / pathLength;

            // The lead ball's final progress (all others are spaced behind it)
            float leadTargetProgress = (chain.Count - 1) * spacingProgress;

            float elapsed = 0f;

            while (elapsed < introDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / introDuration);
                float curveT = introSpeedCurve.Evaluate(t);

                // Animate lead ball from 0 to its target
                float leadProgress = Mathf.Lerp(0f, leadTargetProgress, curveT);

                // Every ball is evenly spaced behind the lead
                for (int i = 0; i < chain.Count; i++)
                {
                    chain[i].pathProgress = leadProgress - (i * spacingProgress);
                }

                ballChainManager.UpdateBallPositionsPublic();
                yield return null;
            }

            // Snap to final positions
            for (int i = 0; i < chain.Count; i++)
            {
                chain[i].pathProgress = leadTargetProgress - (i * spacingProgress);
            }
            ballChainManager.UpdateBallPositionsPublic();
        }
        private void Update()
        {
            if (IsPlayingIntro || ballChainManager == null) return;
            if (ballsSpawned >= totalBallsToSpawn) return;

            if (ballChainManager.NeedsTailBall())
            {
                ballChainManager.SpawnBall(PickColor());
                ballsSpawned++;
            }
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

        public void SetTotalBalls(int total)
        {
            totalBallsToSpawn = Mathf.Max(1, total);
        }
    }
}