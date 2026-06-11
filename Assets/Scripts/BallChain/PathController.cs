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

        [Tooltip("How many samples the spline is baked into at load (a lookup table). Higher = smoother, slightly more memory. 512 is plenty for a smooth path.")]
        [SerializeField] private int lookupResolution = 512;
        // Baked once at load so the per-frame GetPointOnPath/GetDirectionOnPath become cheap array
        // lookups instead of expensive SplineContainer evaluations (the main per-frame cost on WebGL).
        private Vector3[] pointLUT;
        private Vector3[] directionLUT;

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
            BakeLookupTables();
        }

        /// <summary>
        /// Samples the spline once into position + direction lookup tables. Per-frame queries then
        /// interpolate these arrays instead of evaluating the spline, which internally does an
        /// expensive arc-length conversion on every call. Re-run whenever the path is (re)bound.
        /// </summary>
        private void BakeLookupTables()
        {
            int n = Mathf.Max(2, lookupResolution);
            pointLUT = new Vector3[n];
            directionLUT = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float3 pos = splineContainer.EvaluatePosition(t);
                pointLUT[i] = new Vector3(pos.x, pos.y, 0f);
                float3 tan = splineContainer.EvaluateTangent(t);
                Vector3 dir = new Vector3(tan.x, tan.y, 0f);
                directionLUT[i] = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.right;
            }
        }

        /// <summary>
        /// Gets a position along the path based on progress (0-1).
        /// </summary>
        public Vector3 GetPointOnPath(float progress)
        {
            progress = Mathf.Clamp01(progress);

            // Fallback to a direct evaluation before the table is baked (e.g. edit-mode gizmos) or
            // if it somehow ended up empty/too-short — never index a 0-length array.
            if (pointLUT == null || pointLUT.Length < 2)
            {
                if (splineContainer == null) return Vector3.zero;
                float3 p = splineContainer.EvaluatePosition(progress);
                return new Vector3(p.x, p.y, 0f);
            }

            float f = progress * (pointLUT.Length - 1);
            int i = (int)f;
            if (i >= pointLUT.Length - 1) return pointLUT[pointLUT.Length - 1];
            return Vector3.Lerp(pointLUT[i], pointLUT[i + 1], f - i);
        }

        /// <summary>
        /// Gets the direction the path is facing at a given progress point.
        /// </summary>
        public Vector3 GetDirectionOnPath(float progress)
        {
            progress = Mathf.Clamp01(progress);

            // Fallback to a direct evaluation before the table is baked (e.g. edit-mode gizmos) or
            // if it somehow ended up empty/too-short — never index a 0-length array.
            if (directionLUT == null || directionLUT.Length < 2)
            {
                if (splineContainer == null) return Vector3.right;
                float3 tangent = splineContainer.EvaluateTangent(progress);
                Vector3 t = new Vector3(tangent.x, tangent.y, 0f);
                return t.sqrMagnitude > 1e-8f ? t.normalized : Vector3.right;
            }

            float f = progress * (directionLUT.Length - 1);
            int i = (int)f;
            if (i >= directionLUT.Length - 1) return directionLUT[directionLUT.Length - 1];
            return Vector3.Lerp(directionLUT[i], directionLUT[i + 1], f - i).normalized;
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