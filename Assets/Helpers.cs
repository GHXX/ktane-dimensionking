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
    }
}
