using System;

using UnityEngine;
using UnityEngine.UIElements;

using Toolbox;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wsla.Unity
{
    [Serializable]
    public class ChannelsAPI : NetworkAPI.Property
    {
        [field: SerializeField]
        public string[] Names { get; private set; }

        public bool TryGetName(byte index, out string name)
        {
            if (index < 0 || index > Constants.ChannelCount)
                throw new ArgumentOutOfRangeException($"A Valid Channel Index Must be between ({0} & {Constants.ChannelCount - 1})");

            if (Names.IsValidIndex(index) is false)
            {
                name = default;
                return false;
            }

            name = Names[index];
            return true;
        }

        internal void Validate()
        {
            if (Names.Length > Constants.ChannelCount)
            {
                var array = Names;

                Array.Resize(ref array, Constants.ChannelCount);

                Names = array;
            }
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(ChannelsAPI))]
        class Drawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI(SerializedProperty property)
            {
                var Scenes = property.FindBackingFieldRelative(nameof(ChannelsAPI.Names));
                return new UnityEditor.UIElements.PropertyField(Scenes, property.displayName);
            }
        }
#endif
    }
}