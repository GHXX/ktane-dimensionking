using System;
using UnityEngine;

namespace DimensionKing
{
    static class Helpers
    {
        internal static float GetRotationProgress(float percentage, float steepness)
        {
            if (percentage <= 0)
                return 0;

            if (percentage >= 1)
                return 1;

            var a = Mathf.Pow(percentage, steepness);

            return a / (a + Mathf.Pow(1 - percentage, steepness));
        }

        internal static float GetRotationProgress_DER(float x, float steepness)
        {
            if (x <= 0)
                return 0;

            if (x >= 1)
                return 0;

            var oneminusxpowa = Math.Pow(1 - x, steepness);
            var xpowa = Math.Pow(x, steepness);

            return (float)(steepness * xpowa * oneminusxpowa / (x * Math.Pow(oneminusxpowa + xpowa, 2) * (x - 1)));
        }
    }
}
