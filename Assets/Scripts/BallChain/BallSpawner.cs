using UnityEngine;
using System.Collections;

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

            Debug.Log($"Spawned ball #{ballsSpawned} - Color: {randomColor}");
        }

        private BallColor GetRandomColor()
        {
            int colorIndex = Random.Range(0, colorCount);
            return (BallColor)colorIndex;
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
        }
    }
}
