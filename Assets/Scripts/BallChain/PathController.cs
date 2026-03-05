using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Manages the path along which balls move.
    /// Uses Unity Splines for visual editing and smooth curves.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class PathController : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private int gizmoResolution = 50;

        private SplineContainer splineContainer;
        private float totalPathLength;

        private void Awake()
        {
            InitializePath();
        }

        public void InitializePath()
        {
            splineContainer = GetComponent<SplineContainer>();

            if (splineContainer == null || splineContainer.Spline == null)
            {
                Debug.LogError("PathController requires a SplineContainer with a valid spline!");
                return;
            }

            totalPathLength = splineContainer.CalculateLength();
        }

        /// <summary>
        /// Gets a position along the path based on progress (0-1).
        /// </summary>
        public Vector3 GetPointOnPath(float progress)
        {
            if (splineContainer == null) return Vector3.zero;

            progress = Mathf.Clamp01(progress);
            float3 position = splineContainer.EvaluatePosition(progress);
            return new Vector3(position.x, position.y, 0f);
        }

        /// <summary>
        /// Gets the direction the path is facing at a given progress point.
        /// </summary>
        public Vector3 GetDirectionOnPath(float progress)
        {
            if (splineContainer == null) return Vector3.right;

            progress = Mathf.Clamp01(progress);
            float3 tangent = splineContainer.EvaluateTangent(progress);
            Vector3 dir = new Vector3(tangent.x, tangent.y, 0f).normalized;
            return dir;
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
            if (splineContainer == null)
                splineContainer = GetComponent<SplineContainer>();
            if (splineContainer == null || splineContainer.Spline == null) return;

            Gizmos.color = Color.cyan;
            Vector3 prevPoint = GetPointOnPath(0f);

            for (int i = 1; i <= gizmoResolution; i++)
            {
                float t = i / (float)gizmoResolution;
                Vector3 point = GetPointOnPath(t);
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }

            // Draw start and end
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(GetPointOnPath(0f), 0.2f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(GetPointOnPath(1f), 0.2f);
        }
    }
}