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
        [SerializeField] private float recoilDistance = 0.5f;
        [SerializeField] private float recoilDuration = 0.15f;

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
            if (pathController == null || ballChain.Count == 0) return;

            float pathLength = pathController.GetPathLength();
            if (pathLength <= 0) return;

            float moveDistance = ballSpeed * deltaTime;
            float progressIncrease = moveDistance / pathLength;
            float spacingProgress = ballSpacing / pathLength;

            // Move the tail ball (last index, furthest back on path)
            int tailIndex = ballChain.Count - 1;
            ballChain[tailIndex].pathProgress += progressIncrease;

            // Every ball ahead follows spacing from the one behind it
            for (int i = tailIndex - 1; i >= 0; i--)
            {
                float targetProgress = ballChain[i + 1].pathProgress + spacingProgress;

                // If there's a gap (ball is too far ahead), pull it back
                if (ballChain[i].pathProgress > targetProgress)
                {
                    ballChain[i].pathProgress = Mathf.MoveTowards(
                        ballChain[i].pathProgress,
                        targetProgress,
                        gapCloseSpeed * deltaTime / pathLength
                    );
                }
                else
                {
                    // Ball is where it should be or too close — snap to correct spacing
                    ballChain[i].pathProgress = targetProgress;
                }
            }

            // Check if lead ball reached the end
            if (ballChain[0].pathProgress >= 1f)
            {
                ballChain[0].pathProgress = 1f;
                isMoving = false;
                OnBallReachedEnd?.Invoke();
                Debug.LogWarning("Ball reached the end! Game Over!");
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
            int insertIndex = FindNearestBallIndex(insertProgress);

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

            float pathLength = pathController.GetPathLength();
            float spacingProgress = ballSpacing / pathLength;

            // Find the hit ball index
            int hitIndex = FindNearestBallIndex(insertProgress);

            int insertIndex;
            float insertAt;

            if (hitIndex < 0)
            {
                // No balls in chain, just insert at progress
                insertIndex = 0;
                insertAt = insertProgress;
            }
            else
            {
                float hitProgress = ballChain[hitIndex].pathProgress;

                // Determine which side the projectile hit from
                // If projectile progress > hit ball progress, insert ahead (before in list, higher progress)
                // If projectile progress < hit ball progress, insert behind (after in list, lower progress)
                if (insertProgress >= hitProgress)
                {
                    // Insert ahead of the hit ball
                    insertIndex = hitIndex;
                    insertAt = hitProgress + spacingProgress;
                }
                else
                {
                    // Insert behind the hit ball
                    insertIndex = hitIndex + 1;
                    insertAt = hitProgress - spacingProgress;
                }
            }

            BallNode newNode = new BallNode(ball, insertAt, insertIndex);
            ballChain.Insert(insertIndex, newNode);

            // Push balls ahead (lower index = further along path) forward
            PushBallsForward(insertIndex - 1, spacingProgress);

            // Push balls behind (higher index = earlier on path) backward
            PushBallsBackward(insertIndex + 1);

            UpdateChainIndices();

            Debug.Log($"Inserted ball at index {insertIndex} - Color: {color}");

            StartCoroutine(CheckMatchesAfterInsertion(insertIndex));
        }

        /// <summary>
        /// Finds the index of the ball nearest to the given progress.
        /// </summary>
        private int FindNearestBallIndex(float progress)
        {
            if (ballChain.Count == 0) return -1;

            int nearestIndex = 0;
            float nearestDistance = Mathf.Abs(ballChain[0].pathProgress - progress);

            for (int i = 1; i < ballChain.Count; i++)
            {
                float distance = Mathf.Abs(ballChain[i].pathProgress - progress);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        /// <summary>
        /// Pushes balls forward (toward end of path) from the given index.
        /// </summary>
        private void PushBallsForward(int startIndex, float spacingProgress)
        {
            if (startIndex < 0) return;

            for (int i = startIndex; i >= 0; i--)
            {
                float requiredProgress = ballChain[i + 1].pathProgress + spacingProgress;

                if (ballChain[i].pathProgress < requiredProgress)
                {
                    ballChain[i].pathProgress = requiredProgress;
                }
                else
                {
                    break; // Rest of chain already has proper spacing
                }
            }
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

                // Close the gap (wait until spacing has settled)
                yield return StartCoroutine(CloseGap(gapIndex));

                // Only apply recoil when the gap is not at the very front of the line.
                // If gapIndex == 0, there are no balls ahead that were pushed back,
                // so no recoil should occur.
                if (gapIndex > 0)
                {
                    yield return StartCoroutine(ApplyChainRecoil());
                }

                // Check for cascade matches
                if (gapIndex >= 0 && gapIndex < ballChain.Count)
                {
                    matchedBalls = matchDetector.DetectCascadeMatch(ballChain, gapIndex);
                    if (matchedBalls.Count > 0)
                    {
                        Debug.Log($"Cascade match found! {matchedBalls.Count} more balls!");
                    }
                }
                else
                {
                    break; // No more cascades possible
                }
            }
        }

        /// <summary>
        /// Closes the gap after ball removal.
        /// MoveChain handles the actual closing, this just pauses briefly.
        /// </summary>
        private IEnumerator CloseGap(int gapIndex)
        {

            // If gapIndex is invalid or chain too small, nothing to wait for
            if (gapIndex <= 0 || gapIndex >= ballChain.Count) yield break;

            float pathLength = pathController.GetPathLength();
            if (pathLength <= 0f) yield break;

            float spacingProgress = ballSpacing / pathLength;

            float timeout = 1.0f; // safety timeout in seconds
            float elapsed = 0f;

            // Wait until the spacing between the ball ahead of the gap and the ball at the gap
            // reaches the expected spacing (within a small epsilon), or until timeout.
            while (elapsed < timeout)
            {
                if (gapIndex <= 0 || gapIndex >= ballChain.Count) break;

                float desired = ballChain[gapIndex].pathProgress + spacingProgress;
                float current = ballChain[gapIndex - 1].pathProgress;

                if (Mathf.Abs(current - desired) <= 0.0005f)
                {
                    // Spacing settled
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Applies a recoil to the entire chain by moving every ball backward
        /// along the path by a fixed world distance (smoothed over time).
        /// </summary>
        private IEnumerator ApplyChainRecoil()
        {
            if (ballChain.Count == 0) yield break;
            if (recoilDistance <= 0f) yield break;

            float pathLength = pathController.GetPathLength();
            if (pathLength <= 0f) yield break;

            float recoilProgress = recoilDistance / pathLength;

            // Ensure we don't move the lead ball before start of path
            float maxAllowedRecoil = ballChain[0].pathProgress; // lead ball progress
            recoilProgress = Mathf.Min(recoilProgress, maxAllowedRecoil);
            if (recoilProgress <= 0f) yield break;

            // Snapshot the current chain to guard against the list changing during recoil
            BallNode[] snapshot = ballChain.ToArray();
            // Record original progresses from snapshot
            float[] original = new float[snapshot.Length];
            for (int i = 0; i < snapshot.Length; i++) original[i] = snapshot[i].pathProgress;

            float elapsed = 0f;
            while (elapsed < recoilDuration)
            {
                float t = elapsed / recoilDuration;
                float lerp = Mathf.SmoothStep(0f, 1f, t);

                for (int i = 0; i < snapshot.Length; i++)
                {
                    // Update snapshot node progress (safe even if underlying list changes)
                    snapshot[i].pathProgress = original[i] - recoilProgress * lerp;
                }

                // Update visuals based on current ballChain state
                UpdateBallPositions();

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Finalize
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i].pathProgress = original[i] - recoilProgress;
            }
            UpdateBallPositions();
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
