using UnityEngine;
using System.Collections.Generic;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Manages the path along which balls move.
    /// Converts path progress (0-1) to world positions.
    /// </summary>
    public class PathController : MonoBehaviour
    {
        [Header("Path Configuration")]
        [SerializeField] private Transform[] pathPoints;
        [SerializeField] private int pathResolution = 100;
        [SerializeField] private bool drawGizmos = true;

        private Vector3[] pathSegments;
        private float[] segmentLengths;
        private float totalPathLength;

        private void Awake()
        {
            InitializePath();
        }

        /// <summary>
        /// Initializes the path by calculating segments and lengths.
        /// </summary>
        public void InitializePath()
        {
            if (pathPoints == null || pathPoints.Length < 2)
            {
                Debug.LogError("PathController requires at least 2 path points!");
                return;
            }

            CalculatePathSegments();
        }

        /// <summary>
        /// Initializes path from an array of positions (used by LevelManager).
        /// </summary>
        public void InitializePath(Vector3[] points)
        {
            if (points == null || points.Length < 2)
            {
                Debug.LogError("PathController requires at least 2 path points!");
                return;
            }

            // Create path point transforms
            pathPoints = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                GameObject pointObj = new GameObject($"PathPoint_{i}");
                pointObj.transform.SetParent(transform);
                pointObj.transform.position = points[i];
                pathPoints[i] = pointObj.transform;
            }

            CalculatePathSegments();
        }

        private void CalculatePathSegments()
        {
            // Create high-resolution path segments for smooth movement
            List<Vector3> segments = new List<Vector3>();
            List<float> lengths = new List<float>();

            totalPathLength = 0f;

            // Generate segments between each path point
            for (int i = 0; i < pathPoints.Length - 1; i++)
            {
                Vector3 start = pathPoints[i].position;
                Vector3 end = pathPoints[i + 1].position;

                // Linear interpolation between points
                // For curved paths, this could use Bezier curves
                int segmentCount = pathResolution / (pathPoints.Length - 1);

                for (int j = 0; j < segmentCount; j++)
                {
                    float t = j / (float)segmentCount;
                    Vector3 point = Vector3.Lerp(start, end, t);
                    segments.Add(point);

                    // Calculate segment length
                    if (segments.Count > 1)
                    {
                        float segmentLength = Vector3.Distance(
                            segments[segments.Count - 2],
                            segments[segments.Count - 1]
                        );
                        lengths.Add(segmentLength);
                        totalPathLength += segmentLength;
                    }
                }
            }

            // Add final point
            segments.Add(pathPoints[pathPoints.Length - 1].position);
            if (segments.Count > 1)
            {
                float finalLength = Vector3.Distance(
                    segments[segments.Count - 2],
                    segments[segments.Count - 1]
                );
                lengths.Add(finalLength);
                totalPathLength += finalLength;
            }

            pathSegments = segments.ToArray();
            segmentLengths = lengths.ToArray();
        }

        /// <summary>
        /// Gets a position along the path based on progress (0-1).
        /// </summary>
        /// <param name="progress">Progress along path (0 = start, 1 = end)</param>
        /// <returns>World position at that point on the path</returns>
        public Vector3 GetPointOnPath(float progress)
        {
            if (pathSegments == null || pathSegments.Length == 0)
            {
                Debug.LogWarning("Path not initialized!");
                return Vector3.zero;
            }

            // Clamp progress to valid range
            progress = Mathf.Clamp01(progress);

            // Find which segment this progress falls into
            float targetDistance = progress * totalPathLength;
            float currentDistance = 0f;

            for (int i = 0; i < segmentLengths.Length; i++)
            {
                if (currentDistance + segmentLengths[i] >= targetDistance)
                {
                    // Interpolate within this segment
                    float segmentProgress = (targetDistance - currentDistance) / segmentLengths[i];
                    return Vector3.Lerp(pathSegments[i], pathSegments[i + 1], segmentProgress);
                }
                currentDistance += segmentLengths[i];
            }

            // Return end point if we've gone past the path
            return pathSegments[pathSegments.Length - 1];
        }

        /// <summary>
        /// Gets the direction the path is facing at a given progress point.
        /// Useful for orienting balls along the path.
        /// </summary>
        public Vector3 GetDirectionOnPath(float progress)
        {
            float delta = 0.01f;
            Vector3 currentPoint = GetPointOnPath(progress);
            Vector3 nextPoint = GetPointOnPath(progress + delta);

            return (nextPoint - currentPoint).normalized;
        }

        /// <summary>
        /// Gets the total length of the path in world units.
        /// </summary>
        public float GetPathLength()
        {
            return totalPathLength;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Draw path points
            if (pathPoints != null && pathPoints.Length > 0)
            {
                Gizmos.color = Color.yellow;
                foreach (var point in pathPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, 0.2f);
                    }
                }
            }

            // Draw path segments
            if (pathSegments != null && pathSegments.Length > 1)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < pathSegments.Length - 1; i++)
                {
                    Gizmos.DrawLine(pathSegments[i], pathSegments[i + 1]);
                }
            }
        }
    }
}
