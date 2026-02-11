using UnityEngine;
using System.Collections.Generic;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Detects matches of 3 or more consecutive balls of the same color.
    /// Handles cascade detection after ball removal.
    /// </summary>
    public class MatchDetector
    {
        private const int MIN_MATCH_COUNT = 3;

        /// <summary>
        /// Detects matches starting from a specific index in the chain.
        /// Used after ball insertion to check if a match was created.
        /// </summary>
        public List<BallChainManager.BallNode> DetectMatchAtIndex(
            List<BallChainManager.BallNode> chain,
            int centerIndex)
        {
            if (chain == null || chain.Count == 0 || centerIndex < 0 || centerIndex >= chain.Count)
            {
                return new List<BallChainManager.BallNode>();
            }

            BallColor targetColor = chain[centerIndex].ball.BallColor;
            List<BallChainManager.BallNode> matchedBalls = new List<BallChainManager.BallNode>();

            // Expand left from center
            int leftIndex = centerIndex;
            while (leftIndex > 0 && chain[leftIndex - 1].ball.BallColor == targetColor)
            {
                leftIndex--;
            }

            // Expand right from center
            int rightIndex = centerIndex;
            while (rightIndex < chain.Count - 1 && chain[rightIndex + 1].ball.BallColor == targetColor)
            {
                rightIndex++;
            }

            // Calculate match count
            int matchCount = rightIndex - leftIndex + 1;

            // If we have 3 or more, add them to the matched list
            if (matchCount >= MIN_MATCH_COUNT)
            {
                for (int i = leftIndex; i <= rightIndex; i++)
                {
                    matchedBalls.Add(chain[i]);
                }

                Debug.Log($"Match detected! {matchCount} {targetColor} balls from index {leftIndex} to {rightIndex}");
            }

            return matchedBalls;
        }

        /// <summary>
        /// Detects cascade matches after a gap has been closed.
        /// Checks if the balls on either side of the gap now match.
        /// </summary>
        public List<BallChainManager.BallNode> DetectCascadeMatch(
            List<BallChainManager.BallNode> chain,
            int gapIndex)
        {
            if (chain == null || chain.Count == 0 || gapIndex < 0 || gapIndex >= chain.Count)
            {
                return new List<BallChainManager.BallNode>();
            }

            // Check for matches at the point where the gap was closed
            return DetectMatchAtIndex(chain, gapIndex);
        }

        /// <summary>
        /// Detects all matches in the entire chain.
        /// Used for initial level validation or debugging.
        /// </summary>
        public List<List<BallChainManager.BallNode>> DetectAllMatches(
            List<BallChainManager.BallNode> chain)
        {
            List<List<BallChainManager.BallNode>> allMatches = new List<List<BallChainManager.BallNode>>();

            if (chain == null || chain.Count < MIN_MATCH_COUNT)
            {
                return allMatches;
            }

            int i = 0;
            while (i < chain.Count)
            {
                BallColor currentColor = chain[i].ball.BallColor;
                int matchStart = i;
                int matchEnd = i;

                // Find consecutive balls of the same color
                while (matchEnd < chain.Count - 1 &&
                       chain[matchEnd + 1].ball.BallColor == currentColor)
                {
                    matchEnd++;
                }

                int matchCount = matchEnd - matchStart + 1;

                // If we have a match, add it
                if (matchCount >= MIN_MATCH_COUNT)
                {
                    List<BallChainManager.BallNode> match = new List<BallChainManager.BallNode>();
                    for (int j = matchStart; j <= matchEnd; j++)
                    {
                        match.Add(chain[j]);
                    }
                    allMatches.Add(match);
                }

                // Move to the next group
                i = matchEnd + 1;
            }

            return allMatches;
        }

        /// <summary>
        /// Checks if removing balls would create a gap that could lead to a cascade.
        /// Returns the index where the gap would be (for cascade checking).
        /// </summary>
        public int GetGapIndexAfterRemoval(
            List<BallChainManager.BallNode> chain,
            List<BallChainManager.BallNode> ballsToRemove)
        {
            if (chain == null || ballsToRemove == null || ballsToRemove.Count == 0)
            {
                return -1;
            }

            // Find the first ball being removed
            int firstRemovalIndex = chain.IndexOf(ballsToRemove[0]);

            // The gap will be at this index after removal
            // (the ball that comes after the removed section will shift to this position)
            return firstRemovalIndex;
        }

        /// <summary>
        /// Validates if a match list is valid (no duplicates, all same color, min count).
        /// </summary>
        public bool IsValidMatch(List<BallChainManager.BallNode> match)
        {
            if (match == null || match.Count < MIN_MATCH_COUNT)
            {
                return false;
            }

            BallColor firstColor = match[0].ball.BallColor;
            foreach (var node in match)
            {
                if (node.ball.BallColor != firstColor)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
