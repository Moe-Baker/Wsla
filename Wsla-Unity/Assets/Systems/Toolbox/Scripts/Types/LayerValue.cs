using System;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Toolbox
{
    /// <summary>
    /// A value type that will hold a reference to a single layer
    /// </summary>
    [Serializable]
    public struct LayerValue
    {
        [field: SerializeField]
        public int Index { get; private set; }

        public bool IsAssigned => Index >= 0;

        public string Name
        {
            get
            {
                if (IsAssigned == false)
                    throw new NullReferenceException($"Layer Value not Assigned");

                return LayerMask.LayerToName(Index);
            }
        }

        public LayerMask Mask
        {
            get
            {
                if (IsAssigned == false)
                    return 0;

                return 1 << Index;
            }
        }

        public LayerValue(int index)
        {
            this.Index = index;
        }

        public static LayerValue FromName(string value)
        {
            var index = LayerMask.NameToLayer(value);
            return new LayerValue(index);
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(LayerValue))]
        public class Drawer : PropertyDrawer
        {
            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                var index = property.FindBackingFieldRelative(nameof(LayerValue.Index));

                rect = EditorGUI.PrefixLabel(rect, label);

                var content = LayerToGUIContent(index.intValue);

                if (EditorGUI.DropdownButton(rect, content, FocusType.Keyboard))
                {
                    ShowGenericMenu(index.intValue, Callback);
                    void Callback(int target)
                    {
                        index.intValue = target;
                        index.serializedObject.ApplyModifiedProperties();
                    }
                }
            }

            void ShowGenericMenu(int current, Action<int> callback)
            {
                var menu = new GenericMenu();

                //Add None Entry
                {
                    var content = LayerToGUIContent(-1);
                    menu.AddItem(content, current < 0, Surrogate, -1);
                }

                for (int i = 0; i <= 31; i++)
                {
                    var content = LayerToGUIContent(i);
                    menu.AddItem(content, current == i, Surrogate, i);
                }

                void Surrogate(object target)
                {
                    var index = (int)target;
                    callback(index);
                }

                menu.ShowAsContext();
            }

            GUIContent LayerToGUIContent(int index)
            {
                if (index < 0)
                    return new GUIContent("None");

                var name = LayerMask.LayerToName(index);

                if (string.IsNullOrEmpty(name))
                    name = $"Layer {index}";

                return LayerToGUIContent(name);
            }
            GUIContent LayerToGUIContent(string name)
            {
                var content = new GUIContent(name);
                return content;
            }
        }
#endif
    }
}