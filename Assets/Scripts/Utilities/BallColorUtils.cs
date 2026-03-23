using UnityEngine;
using System.Collections.Generic;

namespace YuumisProwl
{
    public static class BallColorUtils
    {
        public static Color ToUnityColor(BallColor ballColor)
        {
            switch (ballColor)
            {
                case BallColor.Red:    return new Color(1f, 0.2f, 0.2f);
                case BallColor.Blue:   return new Color(0.2f, 0.5f, 1f);
                case BallColor.Green:  return new Color(0.3f, 1f, 0.3f);
                case BallColor.Yellow: return new Color(1f, 1f, 0.3f);
                case BallColor.Purple: return new Color(0.8f, 0.3f, 1f);
                case BallColor.Orange: return new Color(1f, 0.6f, 0.2f);
                default:               return Color.white;
            }
        }

        /// <summary>
        /// Returns a random BallColor, avoiding 3-in-a-row runs.
        /// Updates recentColors internally (maintains a window of the last 2 picks).
        /// </summary>
        public static BallColor GetRandomColor(int maxColors, List<BallColor> recentColors)
        {
            if (maxColors <= 1)
            {
                UpdateRecentColors(recentColors, (BallColor)0);
                return (BallColor)0;
            }

            int attempts = 0;
            while (attempts < 10)
            {
                int colorIndex = Random.Range(0, maxColors);
                BallColor candidate = (BallColor)colorIndex;

                if (recentColors.Count == 2)
                {
                    BallColor last = recentColors[recentColors.Count - 1];
                    BallColor secondLast = recentColors[recentColors.Count - 2];
                    if (last == secondLast && last == candidate)
                    {
                        attempts++;
                        continue;
                    }
                }

                UpdateRecentColors(recentColors, candidate);
                return candidate;
            }

            // Fallback: pick any color different from the last
            BallColor lastColor = recentColors.Count > 0 ? recentColors[recentColors.Count - 1] : (BallColor)(-1);
            for (int i = 0; i < maxColors; i++)
            {
                BallColor c = (BallColor)i;
                if (c != lastColor)
                {
                    UpdateRecentColors(recentColors, c);
                    return c;
                }
            }

            UpdateRecentColors(recentColors, (BallColor)0);
            return (BallColor)0;
        }

        private static void UpdateRecentColors(List<BallColor> recentColors, BallColor color)
        {
            recentColors.Add(color);
            if (recentColors.Count > 2) recentColors.RemoveAt(0);
        }
    }
}
