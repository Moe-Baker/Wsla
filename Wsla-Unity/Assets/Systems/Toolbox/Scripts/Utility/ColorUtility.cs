using UnityEngine;

namespace Toolbox
{
    public static class ColorUtility
    {
        public static Color SetAlpha(this Color color, float value)
        {
            color.a = value;

            return color;
        }
    }
}