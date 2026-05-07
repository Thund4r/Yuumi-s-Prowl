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

        public BallNode(Ball ball, float pathProgress, int chainIndex)
        {
            this.ball = ball;
            this.pathProgress = pathProgress;
            this.chainIndex = chainIndex;
            this.segmentId = -1;
            this.smoothShift = 0f;
            this.worldOffset = Vector3.zero;
        }
    }
}
