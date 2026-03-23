using UnityEngine;

namespace YuumisProwl.BallChain
{
    [System.Serializable]
    public class BallNode
    {
        public Ball ball;
        public float pathProgress;
        public int chainIndex;

        public BallNode(Ball ball, float pathProgress, int chainIndex)
        {
            this.ball = ball;
            this.pathProgress = pathProgress;
            this.chainIndex = chainIndex;
        }
    }
}
