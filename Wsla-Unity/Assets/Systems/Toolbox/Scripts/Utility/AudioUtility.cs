using UnityEngine;

namespace Toolbox
{
    public static class AudioUtility
    {
        public static float LinearToDecibel(float linear)
        {
            if (linear == 0)
                return -144.0f;
            else
                return Mathf.Log10(linear) * 20.0f;
        }

        public static float DecibelToLinear(float dB)
        {
            return Mathf.Pow(10.0f, dB / 20.0f);
        }
    }
}