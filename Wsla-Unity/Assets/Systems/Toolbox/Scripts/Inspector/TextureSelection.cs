using System;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Toolbox
{
    /// <summary>
    /// Decorates a field to show a preview for a texture or sprite
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class TextureSelectionAttribute : PropertyAttribute
    {
        public int Lines { get; }

        public TextureSelectionAttribute(int lines = 4)
        {
            this.Lines = lines;
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(TextureSelectionAttribute))]
        public class Drawer : PropertyDrawer
        {
            void GetStyle(out int lines)
            {
                if (attribute is TextureSelectionAttribute selection)
                {
                    lines = selection.Lines;
                    return;
                }

                lines = 4;
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                GetStyle(out var lines);

                if (property.objectReferenceValue == null)
                    return EditorGUIUtility.singleLineHeight;
                else
                    return EditorGUIUtility.singleLineHeight * lines;
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                rect = rect.ZeroIndent();

                if (property.objectReferenceValue == null)
                {
                    EditorGUI.PropertyField(rect, property, label);
                }
                else
                {
                    EditorGUI.BeginProperty(rect, label, property);

                    var target = property.objectReferenceValue;
                    var type = target.GetType();

                    target = EditorGUI.ObjectField(rect, label, target, type, false);

                    property.objectReferenceValue = target;

                    EditorGUI.EndProperty();
                }
            }
        }
#endif
    }
}