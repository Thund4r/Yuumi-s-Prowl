using UnityEngine;

namespace YuumisProwl.VFX
{
    /// <summary>
    /// A green projectile that flies from a destroyed green ball to the boss and, on arrival, adds
    /// poison stacks (rather than dealing damage). Uses the shared HomingBolt curve. Spawned/pooled
    /// by PoisonSynergy; the arrival/lost callbacks let it apply the stacks and recycle the bolt.
    /// </summary>
    public class PoisonBolt : HomingBolt
    {
        private Transform target;
        private int stacks;
        private System.Action<PoisonBolt, int> onArrive;
        private System.Action<PoisonBolt> onLost;

        public void Launch(
            Vector3 origin,
            Transform bossTarget,
            int stackCount,
            float speedUnitsPerSecond,
            float arrivalDist,
            System.Action<PoisonBolt, int> arriveCallback,
            System.Action<PoisonBolt> lostCallback)
        {
            target = bossTarget;
            stacks = stackCount;
            onArrive = arriveCallback;
            onLost = lostCallback;

            Vector3 targetPos = bossTarget != null ? bossTarget.position : origin + Vector3.up;
            Vector3 initialDir = PerpendicularLaunchDir(origin, targetPos);
            BeginFlight(origin, initialDir, speedUnitsPerSecond, arrivalDist);
        }

        protected override Vector3? GetTargetPosition()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                return null;
            return target.position;
        }

        protected override void OnArrived()
        {
            onArrive?.Invoke(this, stacks);
        }

        protected override void OnTargetLost()
        {
            onLost?.Invoke(this);
        }

        public void HardReset()
        {
            StopFlight();
            target = null;
            stacks = 0;
            onArrive = null;
            onLost = null;
            gameObject.SetActive(false);
        }
    }
}
