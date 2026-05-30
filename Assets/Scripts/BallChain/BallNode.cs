using UnityEngine;

namespace YuumisProwl.BallChain
{
    [System.Serializable]
    public class BallNode
    {
        public Ball ball;
        public float pathProgress;
        public int chainIndex;
        public int segmentId;
        /// <summary>
        /// Visual-only path-progress offset. Used by the front balls during insertion so
        /// they keep their old positions and ease forward as the offset decays to zero.
        /// </summary>
        public float smoothShift;
        /// <summary>
        /// Visual-only world-space offset added on top of the path point. Used by a freshly
        /// inserted ball so it visually starts at the projectile's actual world position
        /// (often off the path) and slides toward its target path point as this decays to zero.
        /// </summary>
        public Vector3 worldOffset;
        /// <summary>
        /// Blue Ice-Patches synergy: how many freeze stacks this ball has accrued from
        /// passing through ice patches. At RunConfig.iceFreezeStackThreshold the ball
        /// becomes frozen and stacks zero out.
        /// </summary>
        public int freezeStacks;
        /// <summary>
        /// True if the ball is currently frozen. Frozen balls still move with the chain,
        /// but on destruction (by any means) BallChainManager fires OnFrozenBallDestroyed
        /// so IceSynergy can spawn an icicle.
        /// </summary>
        public bool isFrozen;
        /// <summary>
        /// Orange Conductor synergy: ignite stacks this red ball has accrued from arcs.
        /// At the ignite threshold the ball becomes primed.
        /// </summary>
        public int igniteStacks;
        /// <summary>
        /// True once the ball is primed (ignite threshold reached). A primed ball leaves a
        /// mini-explosion when destroyed by any means — BallChainManager fires
        /// OnIgnitedBallDestroyed so ArcSynergy can detonate it.
        /// </summary>
        public bool primed;
        /// <summary>
        /// Orange Conductor baseline: static stacks applied by arcs to balls whose colour has
        /// no active synergy. At RunConfig.staticThreshold the ball pops (weak single removal).
        /// </summary>
        public int staticStacks;

        public BallNode(Ball ball, float pathProgress, int chainIndex)
        {
            this.ball = ball;
            this.pathProgress = pathProgress;
            this.chainIndex = chainIndex;
            this.segmentId = -1;
            this.smoothShift = 0f;
            this.worldOffset = Vector3.zero;
            this.freezeStacks = 0;
            this.isFrozen = false;
            this.igniteStacks = 0;
            this.primed = false;
            this.staticStacks = 0;
        }
    }
}
