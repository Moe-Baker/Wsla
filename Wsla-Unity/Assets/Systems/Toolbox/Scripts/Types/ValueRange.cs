using System;

using UnityEngine;

namespace Toolbox
{
    [Serializable]
    public struct IntValueRange
    {
        [field: SerializeField]
        public int Min { get; private set; }

        [field: SerializeField]
        public int Max { get; private set; }

        public int Random => UnityEngine.Random.Range(Min, Max + 1);

        public float Lerp(float t) => Mathf.Lerp(Min, Max, t);

        public int Clamp(int value) => Mathf.Clamp(value, Min, Max);
        public float Clamp(float value) => Mathf.Clamp(value, Min, Max);

        public IntValueRange(int min, int max)
        {
            this.Min = min;
            this.Max = max;
        }
    }

    [Serializable]
    public struct FloatValueRange
    {
        [field: SerializeField]
        public float Min { get; private set; }

        [field: SerializeField]
        public float Max { get; private set; }

        public float Random => UnityEngine.Random.Range(Min, Max);

        public float Lerp(float t) => Mathf.Lerp(Min, Max, t);

        public float Clamp(float value) => Mathf.Clamp(value, Min, Max);

        public FloatValueRange(float min, float max)
        {
            this.Min = min;
            this.Max = max;
        }
    }
}