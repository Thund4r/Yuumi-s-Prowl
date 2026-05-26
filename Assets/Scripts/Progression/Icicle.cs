using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Progression
{
    /// <summary>
    /// Blue Ice-Patches synergy projectile. Spawned by IceSynergy when a frozen ball is
    /// destroyed; locks onto a target Ball, homes to it, destroys it on arrival. Chain
    /// reactions happen naturally: if the target was itself frozen, its destruction will
    /// fire OnFrozenBallDestroyed which spawns another icicle.
    ///
    /// Lightweight by design — does not pierce, does not insert, does not interact with
    /// power-ups. Pure single-target destruction. Spawn via IceSynergy, not directly.
    /// </summary>
    public class Icicle : MonoBehaviour
    {
        private Ball target;
        private float speed;
        private float arrivalDistance;
        private BallChainManager chainManager;
        private MatchProcessor matchProcessor;
        private IceSynergy owner;
        private bool active;

        /// <summary>Currently-locked target. Used by IceSynergy to track targeted set.</summary>
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
            transform.position = spawnPosition;
            target = lockedTarget;
            speed = speedUnitsPerSecond;
            arrivalDistance = arrivalDist;
            chainManager = chainMgr;
            matchProcessor = matchProc;
            owner = iceOwner;
            active = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!active) return;

            // Target gone (destroyed by something else, returned to pool, etc.) — give up.
            // The owner will untrack via OnDestroy / explicit release; nothing to destroy.
            if (target == null || !target.gameObject.activeInHierarchy || target.ChainIndex < 0)
            {
                Resolve(false);
                return;
            }

            Vector3 toTarget = target.transform.position - transform.position;
            float dist = toTarget.magnitude;

            if (dist <= arrivalDistance)
            {
                int idx = target.ChainIndex;
                if (chainManager != null && idx >= 0)
                {
                    chainManager.RemoveBallAtIndex(idx);
                    // Hand off to MatchProcessor so cascade detection / chain-cleared logic
                    // runs the same way it does for Bomb / Pierce / red-explosion removals.
                    if (matchProcessor != null)
                        matchProcessor.ProcessPierceAftermath(1);
                }
                Resolve(true);
                return;
            }

            // Aim at the target's live position so chain movement / gap-closing can't shake it.
            Vector3 dir = toTarget / dist;
            transform.position += dir * (speed * Time.deltaTime);

            // Face the direction of travel (2D billboard — same convention as ball rotation).
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        private void Resolve(bool _hitTarget)
        {
            if (!active) return;
            active = false;
            if (owner != null) owner.ReleaseIcicle(this);
        }

        public void HardReset()
        {
            active = false;
            target = null;
            chainManager = null;
            matchProcessor = null;
            owner = null;
            gameObject.SetActive(false);
        }
    }
}
