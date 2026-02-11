using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YuumisProwl.Utilities;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Manages the chain of balls moving along the path.
    /// Handles ball movement, insertion, removal, and spacing.
    /// </summary>
    public class BallChainManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PathController pathController;
        [SerializeField] private Ball ballPrefab;

        [Header("Movement Settings")]
        [SerializeField] private float ballSpeed = 2f;
        [SerializeField] private float ballSpacing = 0.5f;

        [Header("Match Settings")]
        [SerializeField] private float gapCloseSpeed = 5f;
        [SerializeField] private float destructionDelay = 0.1f;
        [SerializeField] private bool enableDestructionEffects = true;

        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 50;

        // Events
        public System.Action<int, BallColor> OnBallsDestroyed;
        public System.Action OnChainCleared;
        public System.Action OnBallReachedEnd;

        // Ball chain data structure
        [System.Serializable]
        public class BallNode
        {
            public Ball ball;
            public float pathProgress;
            public int chainIndex;

            public BallNode(Ball ball, float pathProgress, int chainIndex)
            {
                this.ball = ball;
                this.pathProgress = pathProgress;
                this.chainIndex = chainIndex;
            }
        }

        private List<BallNode> ballChain = new List<BallNode>();
        private ObjectPool<Ball> ballPool;
        private MatchDetector matchDetector;
        private bool isMoving = true;
        private bool isProcessingMatches = false;

        public float BallSpeed => ballSpeed;
        public float BallSpacing => ballSpacing;
        public int BallCount => ballChain.Count;
        public bool IsProcessingMatches => isProcessingMatches;

        private void Awake()
        {
            InitializePool();
            matchDetector = new MatchDetector();
        }

        private void Start()
        {
            if (pathController == null)
            {
                Debug.LogError("BallChainManager: PathController not assigned!");
                enabled = false;
            }
        }

        private void Update()
        {
            if (isMoving)
            {
                MoveChain(Time.deltaTime);
                UpdateBallPositions();
            }
        }

        private void InitializePool()
        {
            if (ballPrefab == null)
            {
                Debug.LogError("BallChainManager: Ball prefab not assigned!");
                return;
            }

            ballPool = new ObjectPool<Ball>(ballPrefab, initialPoolSize, transform);
        }

        /// <summary>
        /// Moves the entire chain forward along the path.
        /// </summary>
        private void MoveChain(float deltaTime)
        {
            if (pathController == null) return;

            float pathLength = pathController.GetPathLength();
            if (pathLength <= 0) return;

            float moveDistance = ballSpeed * deltaTime;
            float progressIncrease = moveDistance / pathLength;

            for (int i = 0; i < ballChain.Count; i++)
            {
                ballChain[i].pathProgress += progressIncrease;

                // Check if ball reached the end
                if (ballChain[i].pathProgress >= 1f)
                {
                    ballChain[i].pathProgress = 1f;
                    isMoving = false;
                    OnBallReachedEnd?.Invoke();
                    Debug.LogWarning("Ball reached the end! Game Over!");
                }
            }
        }

        /// <summary>
        /// Updates the visual positions of all balls based on their path progress.
        /// </summary>
        private void UpdateBallPositions()
        {
            foreach (var node in ballChain)
            {
                if (node.ball != null)
                {
                    node.ball.transform.position = pathController.GetPointOnPath(node.pathProgress);
                    node.ball.PathProgress = node.pathProgress;

                    // Orient ball along path direction
                    Vector3 direction = pathController.GetDirectionOnPath(node.pathProgress);
                    if (direction != Vector3.zero)
                    {
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        node.ball.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
                    }
                }
            }
        }

        /// <summary>
        /// Spawns a new ball at the start of the path.
        /// </summary>
        public void SpawnBall(BallColor color)
        {
            Ball ball = ballPool.Get();
            ball.Initialize(color);
            ball.OnGetFromPool();

            // Calculate spawn position
            float spawnProgress = CalculateSpawnProgress();

            BallNode newNode = new BallNode(ball, spawnProgress, ballChain.Count);
            ballChain.Add(newNode);

            ball.ChainIndex = newNode.chainIndex;
            UpdateChainIndices();
        }

        /// <summary>
        /// Calculates the spawn progress for a new ball at the end of the chain.
        /// </summary>
        private float CalculateSpawnProgress()
        {
            if (ballChain.Count == 0)
            {
                return 0f;
            }

            // Spawn behind the last ball
            BallNode lastBall = ballChain[ballChain.Count - 1];
            float pathLength = pathController.GetPathLength();
            float spacingProgress = ballSpacing / pathLength;

            return lastBall.pathProgress - spacingProgress;
        }

        /// <summary>
        /// Inserts a ball into the chain at a specific progress point.
        /// Used when projectiles hit the chain.
        /// </summary>
        public void InsertBall(Ball newBall, float insertProgress)
        {
            // Find insertion index
            int insertIndex = FindInsertionIndex(insertProgress);

            // Create new node
            BallNode newNode = new BallNode(newBall, insertProgress, insertIndex);

            // Insert into chain
            ballChain.Insert(insertIndex, newNode);

            // Push subsequent balls backward to maintain spacing
            PushBallsBackward(insertIndex + 1);

            // Update all chain indices
            UpdateChainIndices();
        }

        /// <summary>
        /// Spawns a ball from the pool and inserts it at a specific progress point.
        /// Used by projectiles when they hit the chain.
        /// </summary>
        public void InsertBallAtProgress(BallColor color, float insertProgress)
        {
            Ball ball = ballPool.Get();
            ball.Initialize(color);
            ball.OnGetFromPool();

            // Find insertion index before inserting
            int insertIndex = FindInsertionIndex(insertProgress);

            InsertBall(ball, insertProgress);

            Debug.Log($"Inserted ball at progress {insertProgress:F2} - Color: {color}");

            // Check for matches after insertion
            StartCoroutine(CheckMatchesAfterInsertion(insertIndex));
        }

        /// <summary>
        /// Finds the correct index to insert a ball based on path progress.
        /// </summary>
        private int FindInsertionIndex(float progress)
        {
            for (int i = 0; i < ballChain.Count; i++)
            {
                if (ballChain[i].pathProgress > progress)
                {
                    return i;
                }
            }
            return ballChain.Count;
        }

        /// <summary>
        /// Pushes balls backward from the insertion point to maintain proper spacing.
        /// </summary>
        private void PushBallsBackward(int startIndex)
        {
            if (startIndex >= ballChain.Count) return;

            float pathLength = pathController.GetPathLength();
            float spacingProgress = ballSpacing / pathLength;

            for (int i = startIndex; i < ballChain.Count; i++)
            {
                // Calculate required spacing from previous ball
                float requiredProgress = ballChain[i - 1].pathProgress - spacingProgress;

                // Only push if too close
                if (ballChain[i].pathProgress > requiredProgress)
                {
                    ballChain[i].pathProgress = requiredProgress;
                }
                else
                {
                    // Rest of chain has proper spacing
                    break;
                }
            }
        }

        /// <summary>
        /// Updates chain indices after insertion/removal.
        /// </summary>
        private void UpdateChainIndices()
        {
            for (int i = 0; i < ballChain.Count; i++)
            {
                ballChain[i].chainIndex = i;
                if (ballChain[i].ball != null)
                {
                    ballChain[i].ball.ChainIndex = i;
                }
            }
        }

        /// <summary>
        /// Checks for matches after ball insertion and handles cascades.
        /// </summary>
        private IEnumerator CheckMatchesAfterInsertion(int insertedIndex)
        {
            if (isProcessingMatches) yield break;

            isProcessingMatches = true;
            yield return new WaitForSeconds(destructionDelay);

            // Check for matches at the inserted position
            List<BallNode> matchedBalls = matchDetector.DetectMatchAtIndex(ballChain, insertedIndex);

            if (matchedBalls.Count > 0)
            {
                yield return StartCoroutine(ProcessMatches(matchedBalls));
            }

            isProcessingMatches = false;
        }

        /// <summary>
        /// Processes matched balls, removes them, and checks for cascades.
        /// </summary>
        private IEnumerator ProcessMatches(List<BallNode> matchedBalls)
        {
            while (matchedBalls.Count > 0)
            {
                // Get gap index before removal
                int gapIndex = matchDetector.GetGapIndexAfterRemoval(ballChain, matchedBalls);
                BallColor matchedColor = matchedBalls[0].ball.BallColor;
                int matchCount = matchedBalls.Count;

                // Remove the matched balls
                RemoveBalls(matchedBalls);

                // Notify listeners (for scoring)
                OnBallsDestroyed?.Invoke(matchCount, matchedColor);
                Debug.Log($"Destroyed {matchCount} {matchedColor} balls!");

                // Check if chain is cleared
                if (ballChain.Count == 0)
                {
                    OnChainCleared?.Invoke();
                    Debug.Log("All balls cleared! Level Complete!");
                    yield break;
                }

                // Close the gap
                yield return StartCoroutine(CloseGap(gapIndex));

                // Check for cascade matches
                if (gapIndex >= 0 && gapIndex < ballChain.Count)
                {
                    matchedBalls = matchDetector.DetectCascadeMatch(ballChain, gapIndex);
                    if (matchedBalls.Count > 0)
                    {
                        Debug.Log($"Cascade match found! {matchedBalls.Count} more balls!");
                        yield return new WaitForSeconds(destructionDelay);
                    }
                }
                else
                {
                    break; // No more cascades possible
                }
            }
        }

        /// <summary>
        /// Closes the gap after ball removal by pulling balls together.
        /// </summary>
        private IEnumerator CloseGap(int gapIndex)
        {
            if (gapIndex < 0 || gapIndex >= ballChain.Count) yield break;

            float pathLength = pathController.GetPathLength();
            float spacingProgress = ballSpacing / pathLength;

            // Calculate target positions for all balls after the gap
            List<float> targetProgresses = new List<float>();
            for (int i = gapIndex; i < ballChain.Count; i++)
            {
                if (i == gapIndex && gapIndex > 0)
                {
                    // First ball after gap should be spaced from previous ball
                    targetProgresses.Add(ballChain[gapIndex - 1].pathProgress - spacingProgress);
                }
                else if (i == gapIndex && gapIndex == 0)
                {
                    // If gap is at start, just use current progress
                    targetProgresses.Add(ballChain[i].pathProgress);
                }
                else
                {
                    // Subsequent balls maintain spacing
                    targetProgresses.Add(targetProgresses[i - gapIndex] - spacingProgress);
                }
            }

            // Animate gap closing
            float elapsed = 0f;
            float duration = 0.3f; // Gap closing animation duration

            List<float> startProgresses = new List<float>();
            for (int i = gapIndex; i < ballChain.Count; i++)
            {
                startProgresses.Add(ballChain[i].pathProgress);
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                for (int i = gapIndex; i < ballChain.Count; i++)
                {
                    int localIndex = i - gapIndex;
                    ballChain[i].pathProgress = Mathf.Lerp(
                        startProgresses[localIndex],
                        targetProgresses[localIndex],
                        t
                    );
                }

                yield return null;
            }

            // Ensure final positions are exact
            for (int i = gapIndex; i < ballChain.Count; i++)
            {
                ballChain[i].pathProgress = targetProgresses[i - gapIndex];
            }
        }

        /// <summary>
        /// Removes balls from the chain and returns them to the pool.
        /// </summary>
        public void RemoveBalls(List<BallNode> nodesToRemove)
        {
            foreach (var node in nodesToRemove)
            {
                if (node.ball != null)
                {
                    // Play destruction effect (optional - can be enhanced with particle systems)
                    if (enableDestructionEffects)
                    {
                        // Simple scale-down animation before removal
                        // In production, you'd spawn a particle effect here
                        Debug.Log($"💥 Destroying {node.ball.BallColor} ball at position {node.ball.transform.position}");
                    }

                    ballPool.Return(node.ball);
                    node.ball.OnReturnToPool();
                }
                ballChain.Remove(node);
            }

            UpdateChainIndices();
        }

        /// <summary>
        /// Sets the movement speed of the ball chain.
        /// </summary>
        public void SetSpeed(float speed)
        {
            ballSpeed = speed;
        }

        /// <summary>
        /// Pauses or resumes ball chain movement.
        /// </summary>
        public void SetMoving(bool moving)
        {
            isMoving = moving;
        }

        /// <summary>
        /// Gets the ball chain for external access (e.g., match detection).
        /// </summary>
        public List<BallNode> GetBallChain()
        {
            return ballChain;
        }

        /// <summary>
        /// Clears all balls from the chain.
        /// </summary>
        public void ClearChain()
        {
            foreach (var node in ballChain)
            {
                if (node.ball != null)
                {
                    ballPool.Return(node.ball);
                    node.ball.OnReturnToPool();
                }
            }
            ballChain.Clear();
        }

        private void OnDestroy()
        {
            ClearChain();
        }
    }
}
