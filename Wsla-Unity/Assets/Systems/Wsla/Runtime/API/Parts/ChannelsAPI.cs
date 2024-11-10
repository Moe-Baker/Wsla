using System;

using UnityEngine;
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
            void Init(SerializedProperty property, out SerializedProperty names)
            {
                names = property.FindBackingFieldRelative(nameof(ChannelsAPI.Names));
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                Init(property, out var names);

                return EditorGUI.GetPropertyHeight(names, label, true);
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                Init(property, out var names);

                EditorGUI.PropertyField(rect, names, label, true);
            }
        }
#endif
    }
}