using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YuumisProwl;
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

        [Header("Gap Close Settings")]
        [SerializeField] private float gapCloseSpeed = 5f;

        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 50;

        [Header("Spawn Hole Settings")]
        [SerializeField] private float holeProgress = 0f; // Progress at which balls disappear/appear

        // Events
        public System.Action OnBallReachedEnd;
        public System.Action<int> OnBallInserted;

        private List<BallNode> ballChain = new List<BallNode>();
        private ObjectPool<Ball> ballPool;
        private bool isMoving = true;

        public float BallSpeed => ballSpeed;
        public float BallSpacing => ballSpacing;
        public int BallCount => ballChain.Count;

        private void Awake()
        {
            InitializePool();
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
                UpdateBallVisibility();
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
                if (node.ball != null && node.ball.gameObject.activeSelf)
                {
                    node.ball.transform.position = pathController.GetPointOnPath(Mathf.Max(node.pathProgress, holeProgress));
                    node.ball.PathProgress = node.pathProgress;

                    Vector3 direction = pathController.GetDirectionOnPath(Mathf.Max(node.pathProgress, holeProgress));
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

            OnBallInserted?.Invoke(insertIndex);
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
        /// Removes balls from the chain and returns them to the pool.
        /// </summary>
        public void RemoveBalls(List<BallNode> nodesToRemove)
        {
            foreach (var node in nodesToRemove)
            {
                if (node.ball != null)
                {
                    ballPool.Return(node.ball);
                    node.ball.OnReturnToPool();
                }
                ballChain.Remove(node);
            }

            UpdateChainIndices();
        }

        /// <summary>
        /// Updates visibility of all balls based on whether they're past the hole.
        /// </summary>
        private void UpdateBallVisibility()
        {
            foreach (var node in ballChain)
            {
                UpdateBallVisibility(node);
            }
        }

        private void UpdateBallVisibility(BallNode node)
        {
            if (node.ball == null) return;

            bool shouldBeVisible = node.pathProgress >= holeProgress;
            if (node.ball.gameObject.activeSelf != shouldBeVisible)
            {
                node.ball.gameObject.SetActive(shouldBeVisible);
            }
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
        /// Public wrapper so external systems (like intro animation) can update visuals.
        /// </summary>
        public void UpdateBallPositionsPublic()
        {
            UpdateBallPositions();
        }

        public void UpdateBallVisibilityPublic()
        {
            UpdateBallVisibility();
        }

        /// <summary>
        /// Returns true when the tail ball has moved far enough that a new ball should be spawned behind it.
        /// </summary>
        public bool NeedsTailBall()
        {
            if (ballChain.Count == 0) return false;

            float pathLength = pathController.GetPathLength();
            if (pathLength <= 0f) return false;

            float spacingProgress = ballSpacing / pathLength;
            BallNode tail = ballChain[ballChain.Count - 1];
            return tail.pathProgress > holeProgress + spacingProgress;
        }

        public float GetPathLength()
        {
            return pathController != null ? pathController.GetPathLength() : 0f;
        }

        /// <summary>
        /// Returns true if any ball in the chain is past the hole (i.e. visible on screen).
        /// Used by GameManager to detect the "retreat into hole" win condition.
        /// </summary>
        public bool HasVisibleBalls()
        {
            foreach (var node in ballChain)
            {
                if (node.pathProgress >= holeProgress)
                    return true;
            }
            return false;
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
