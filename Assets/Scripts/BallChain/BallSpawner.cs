using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Spawns balls and manages the intro animation.
    /// </summary>
    public class BallSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;

        [Header("Spawn Settings")]
        [SerializeField] private int ballCount = 30;
        [SerializeField] private int colorCount = 4;

        [Header("Intro Animation")]
        [SerializeField] private float introDuration = 7f;
        [SerializeField] private AnimationCurve introSpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private List<BallColor> recentColors = new List<BallColor>(2);

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
                return;
            }

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
            for (int i = 0; i < ballCount; i++)
            {
                BallColor color = GetRandomColor();
                ballChainManager.SpawnBall(color);

                recentColors.Add(color);
                if (recentColors.Count > 2)
                    recentColors.RemoveAt(0);
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
        private BallColor GetRandomColor()
        {
            if (colorCount <= 1)
                return (BallColor)0;

            int attempts = 0;
            while (attempts < 10)
            {
                int colorIndex = Random.Range(0, colorCount);
                BallColor candidate = (BallColor)colorIndex;

                if (recentColors.Count == 2)
                {
                    BallColor last = recentColors[recentColors.Count - 1];
                    BallColor secondLast = recentColors[recentColors.Count - 2];
                    if (last == secondLast && last == candidate)
                    {
                        attempts++;
                        continue;
                    }
                }
                return candidate;
            }

            BallColor lastColor = recentColors.Count > 0 ? recentColors[recentColors.Count - 1] : (BallColor)(-1);
            for (int i = 0; i < colorCount; i++)
            {
                BallColor c = (BallColor)i;
                if (c != lastColor) return c;
            }
            return (BallColor)0;
        }

        public void SetColorCount(int count)
        {
            colorCount = Mathf.Clamp(count, 1, 6);
        }
    }
}