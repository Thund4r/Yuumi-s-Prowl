using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Spawns balls at regular intervals for testing and gameplay.
    /// </summary>
    public class BallSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallChainManager ballChainManager;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnInterval = 1f;
        [SerializeField] private int maxBalls = 30;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private int colorCount = 4;

        [Header("Test Controls")]
        [SerializeField] private bool autoSpawn = true;
        [SerializeField] private KeyCode spawnKey = KeyCode.Space;

        private float spawnTimer;
        private int ballsSpawned;
        // Keep the last two spawned colors to avoid creating 3-in-a-row
        private List<BallColor> recentColors = new List<BallColor>(2);

        private void Start()
        {
            if (ballChainManager == null)
            {
                Debug.LogError("BallSpawner: BallChainManager not assigned!");
                enabled = false;
                return;
            }

            if (spawnOnStart)
            {
                StartCoroutine(SpawnInitialBalls());
            }
        }

        private void Update()
        {
            // Manual spawn with key press
            if (Input.GetKeyDown(spawnKey))
            {
                SpawnBall();
            }

            // Auto spawn
            if (autoSpawn && ballsSpawned < maxBalls)
            {
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= spawnInterval)
                {
                    SpawnBall();
                    spawnTimer = 0f;
                }
            }
        }

        private IEnumerator SpawnInitialBalls()
        {
            // Spawn a few balls at the start with proper spacing
            int initialCount = Mathf.Min(5, maxBalls);

            for (int i = 0; i < initialCount; i++)
            {
                SpawnBall();
                yield return new WaitForSeconds(0.2f);
            }
        }

        private void SpawnBall()
        {
            if (ballsSpawned >= maxBalls) return;

            BallColor randomColor = GetRandomColor();
            ballChainManager.SpawnBall(randomColor);
            ballsSpawned++;

            // Track recent colors (max 2)
            recentColors.Add(randomColor);
            if (recentColors.Count > 2)
            {
                recentColors.RemoveAt(0);
            }

            Debug.Log($"Spawned ball #{ballsSpawned} - Color: {randomColor}");
        }

        private BallColor GetRandomColor()
        {
            if (colorCount <= 1)
            {
                return (BallColor)0;
            }

            int attempts = 0;
            while (attempts < 10)
            {
                int colorIndex = Random.Range(0, colorCount);
                BallColor candidate = (BallColor)colorIndex;

                // If last two exist and are the same as candidate, try again
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

            // Fallback: pick any color different from the last one if possible
            BallColor lastColor = recentColors.Count > 0 ? recentColors[recentColors.Count - 1] : (BallColor)(-1);
            for (int i = 0; i < colorCount; i++)
            {
                BallColor c = (BallColor)i;
                if (c != lastColor) return c;
            }
            return (BallColor)0;
        }

        /// <summary>
        /// Sets the number of different colors to use.
        /// </summary>
        public void SetColorCount(int count)
        {
            colorCount = Mathf.Clamp(count, 1, 6);
        }

        /// <summary>
        /// Resets the spawner.
        /// </summary>
        public void Reset()
        {
            ballsSpawned = 0;
            spawnTimer = 0f;
            recentColors.Clear();
        }
    }
}
