using UnityEngine;
using YuumisProwl.BallChain;
using YuumisProwl.VFX;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Blue Ice-Patches synergy projectile. Spawned by IceSynergy when a frozen ball is
    /// destroyed; locks onto a target Ball and destroys it on arrival. Chain reactions happen
    /// naturally: if the target was itself frozen, its destruction is reported via
    /// BallChainManager.OnBallsDestroyed (wasFrozen), which spawns another icicle.
    ///
    /// Flight is the shared HomingBolt curve — it kicks out perpendicular to the spawn→target
    /// line, then curves in steeply and hits. Lightweight by design: does not pierce, insert, or
    /// interact with power-ups. Spawn via IceSynergy, not directly.
    /// </summary>
    public class Icicle : HomingBolt
    {
        private Ball target;
        private BallChainManager chainManager;
        private MatchProcessor matchProcessor;
        private IceSynergy owner;

        /// <summary>Currently-locked target. Used by IceSynergy to track the targeted set.</summary>
        public Ball Target => target;

        public void Launch(
            Vector3 spawnPosition,
            Ball lockedTarget,
            float speedUnitsPerSecond,
            float arrivalDist,
            BallChainManager chainMgr,
            MatchProcessor matchProc,
            IceSynergy iceOwner)
        {
            target = lockedTarget;
            chainManager = chainMgr;
            matchProcessor = matchProc;
            owner = iceOwner;

            Vector3 targetPos = lockedTarget != null ? lockedTarget.transform.position : spawnPosition + Vector3.up;
            Vector3 initialDir = PerpendicularLaunchDir(spawnPosition, targetPos);
            BeginFlight(spawnPosition, initialDir, speedUnitsPerSecond, arrivalDist);
        }

        protected override Vector3? GetTargetPosition()
        {
            // Target gone (destroyed by something else, returned to pool, etc.) — give up.
            if (target == null || !target.gameObject.activeInHierarchy || target.ChainIndex < 0)
                return null;
            // Aim at the target's live position so chain movement / gap-closing can't shake it.
            return target.transform.position;
        }

        protected override void OnArrived()
        {
            int idx = target != null ? target.ChainIndex : -1;
            if (chainManager != null && idx >= 0)
            {
                chainManager.RemoveBallAtIndex(idx);
                // Hand off to MatchProcessor so cascade detection / chain-cleared logic runs the
                // same way it does for Bomb / Pierce / red-explosion removals.
                if (matchProcessor != null)
                    matchProcessor.ProcessPierceAftermath(1);
            }
            if (owner != null) owner.ReleaseIcicle(this);
        }

        protected override void OnTargetLost()
        {
            if (owner != null) owner.ReleaseIcicle(this);
        }

        public void HardReset()
        {
            StopFlight();
            target = null;
            chainManager = null;
            matchProcessor = null;
            owner = null;
            gameObject.SetActive(false);
        }
    }
}
