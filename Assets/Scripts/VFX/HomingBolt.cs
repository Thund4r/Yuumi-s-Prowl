using UnityEngine;

namespace YuumisProwl.VFX
{
    /// <summary>
    /// Reusable curved-flight projectile. It first travels straight along a launch direction
    /// (typically perpendicular to the spawn→target line — the visible "shoot out sideways" kick),
    /// then homes onto a live target with a steep turn and a guaranteed finish (it homes
    /// dead-straight once close, so it can't orbit and miss).
    ///
    /// Subclasses supply the target position and the arrival / lost outcomes. Used by the Blue
    /// synergy Icicle and reusable for a ball→boss damage bolt.
    /// </summary>
    public abstract class HomingBolt : MonoBehaviour
    {
        [Header("Flight")]
        [Tooltip("Seconds the bolt travels straight along its launch direction before it begins homing — the visible 'kick out sideways' phase.")]
        [SerializeField, Min(0f)] protected float launchDuration = 0.1f;
        [Tooltip("How fast the heading rotates toward the target once homing (deg/sec). Higher = steeper, snappier curve.")]
        [SerializeField, Min(1f)] protected float turnSpeedDeg = 540f;
        [Tooltip("Multiple of arrival distance within which the bolt homes dead-straight, guaranteeing the hit (no orbiting).")]
        [SerializeField, Min(1f)] protected float straightenArrivalMultiple = 3f;

        protected Vector3 heading;
        protected float speed;
        protected float arrivalDistance;
        private float elapsed;
        private bool active;

        protected bool IsFlying => active;

        /// <summary>
        /// Begins a flight from spawnPos along initialDir (need not be normalized), at the given
        /// speed, resolving when within arrivalDist of the live target. Activates the GameObject.
        /// </summary>
        protected void BeginFlight(Vector3 spawnPos, Vector3 initialDir, float speedUnitsPerSecond, float arrivalDist)
        {
            transform.position = spawnPos;
            heading = initialDir.sqrMagnitude > 1e-6f ? initialDir.normalized : Vector3.up;
            speed = speedUnitsPerSecond;
            arrivalDistance = Mathf.Max(0.01f, arrivalDist);
            elapsed = 0f;
            active = true;
            FaceHeading();
            gameObject.SetActive(true);
        }

        protected void StopFlight() { active = false; }

        /// <summary>Live target world position, or null if the target is lost (fires OnTargetLost).</summary>
        protected abstract Vector3? GetTargetPosition();
        /// <summary>Fired once when the bolt reaches its target.</summary>
        protected abstract void OnArrived();
        /// <summary>Fired once when the target is lost before arrival.</summary>
        protected abstract void OnTargetLost();

        private void Update()
        {
            if (!active) return;

            Vector3? tp = GetTargetPosition();
            if (tp == null) { active = false; OnTargetLost(); return; }

            Vector3 toTarget = tp.Value - transform.position;
            float dist = toTarget.magnitude;

            if (dist <= arrivalDistance) { active = false; OnArrived(); return; }

            elapsed += Time.deltaTime;

            // Launch phase: keep the straight perpendicular heading so the kick is visible. After
            // it, curve hard toward the live target; once close, snap straight to guarantee the hit.
            if (elapsed >= launchDuration)
            {
                Vector3 desired = toTarget / dist;
                if (dist <= arrivalDistance * straightenArrivalMultiple)
                {
                    heading = desired;
                }
                else
                {
                    float maxRad = turnSpeedDeg * Mathf.Deg2Rad * Time.deltaTime;
                    heading = Vector3.RotateTowards(heading, desired, maxRad, 0f).normalized;
                }
            }

            transform.position += heading * (speed * Time.deltaTime);
            FaceHeading();
        }

        /// <summary>Points the transform along the current heading (2D billboard, ball-rotation convention).</summary>
        protected void FaceHeading()
        {
            float angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        /// <summary>
        /// A unit vector perpendicular to the spawn→target line, on a random side — the default
        /// launch direction for the "kick out then curve in" look. Falls back to up if degenerate.
        /// </summary>
        public static Vector3 PerpendicularLaunchDir(Vector3 spawn, Vector3 target)
        {
            Vector3 d = target - spawn;
            d.z = 0f;
            if (d.sqrMagnitude < 1e-6f) return Vector3.up;
            d.Normalize();
            Vector3 perp = new Vector3(-d.y, d.x, 0f);
            return (Random.value < 0.5f) ? perp : -perp;
        }
    }
}
