using UnityEngine;

namespace YuumisProwl.VFX
{
    /// <summary>
    /// A damage projectile that flies from where balls were destroyed to the boss, then deals the
    /// carried damage on arrival. Uses the shared HomingBolt curve (kick out perpendicular, then
    /// curve in steeply and hit). Spawned/pooled by BossManager; the arrival/lost callbacks let
    /// BossManager apply the damage and recycle the bolt.
    /// </summary>
    public class BossDamageBolt : HomingBolt
    {
        [Header("Damage Scaling")]
        [Tooltip("Scale multiplier for a 0-damage 'fizzle' bolt (e.g. one fully absorbed by a Warden shield).")]
        [SerializeField, Min(0f)] private float minScaleMultiplier = 0.5f;
        [Tooltip("Added scale multiplier per point of damage beyond 1, so a bigger hit reads as a bigger bolt.")]
        [SerializeField, Min(0f)] private float scalePerDamage = 0.15f;
        [Tooltip("Maximum scale multiplier, so a huge hit can't make the bolt absurdly large.")]
        [SerializeField, Min(1f)] private float maxScaleMultiplier = 3f;

        private Transform target;
        private float damage;
        private System.Action<BossDamageBolt, float> onArrive;
        private System.Action<BossDamageBolt> onLost;
        private Vector3 baseScale = Vector3.one;
        private bool baseScaleCaptured;

        private void Awake()
        {
            baseScale = transform.localScale;
            baseScaleCaptured = true;
        }

        public void Launch(
            Vector3 origin,
            Transform bossTarget,
            float dmg,
            float speedUnitsPerSecond,
            float arrivalDist,
            System.Action<BossDamageBolt, float> arriveCallback,
            System.Action<BossDamageBolt> lostCallback)
        {
            target = bossTarget;
            damage = dmg;
            onArrive = arriveCallback;
            onLost = lostCallback;

            if (!baseScaleCaptured) { baseScale = transform.localScale; baseScaleCaptured = true; }
            float mult = dmg <= 0f
                ? minScaleMultiplier
                : Mathf.Min(maxScaleMultiplier, 1f + (dmg - 1f) * scalePerDamage);
            transform.localScale = baseScale * mult;

            Vector3 targetPos = bossTarget != null ? bossTarget.position : origin + Vector3.up;
            Vector3 initialDir = PerpendicularLaunchDir(origin, targetPos);
            BeginFlight(origin, initialDir, speedUnitsPerSecond, arrivalDist);
        }

        protected override Vector3? GetTargetPosition()
        {
            // Boss gone (defeated / map torn down) — give up; the carried damage is dropped.
            if (target == null || !target.gameObject.activeInHierarchy)
                return null;
            return target.position;
        }

        protected override void OnArrived()
        {
            onArrive?.Invoke(this, damage);
        }

        protected override void OnTargetLost()
        {
            onLost?.Invoke(this);
        }

        public void HardReset()
        {
            StopFlight();
            target = null;
            damage = 0;
            onArrive = null;
            onLost = null;
            gameObject.SetActive(false);
        }
    }
}
