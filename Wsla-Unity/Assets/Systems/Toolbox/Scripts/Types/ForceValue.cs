using System;

using UnityEngine;

namespace Toolbox
{
    [Serializable]
    public struct ForceValue
    {
        [field: SerializeField]
        public float Value { get; private set; }

        [field: SerializeField]
        public ForceMode Mode { get; private set; }

        public ForceValue(float value, ForceMode mode)
        {
            this.Value = value;
            this.Mode = mode;
        }
    }
}