using System.Collections.Generic;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// A contiguous group of balls along the path. The chain may consist of multiple
    /// segments separated by gaps (e.g. after a Bomb explosion). Each segment moves,
    /// recoils, and matches independently.
    ///
    /// Within a segment, balls are ordered front-to-back (index 0 = lead = highest progress,
    /// index Count-1 = tail = lowest progress).
    /// </summary>
    public class ChainSegment
    {
        public int id;
        public List<BallNode> balls = new List<BallNode>();

        public ChainSegment(int id)
        {
            this.id = id;
        }

        public int Count => balls.Count;
        public bool IsEmpty => balls.Count == 0;

        public BallNode Lead => balls.Count > 0 ? balls[0] : null;
        public BallNode Tail => balls.Count > 0 ? balls[balls.Count - 1] : null;
    }
}
