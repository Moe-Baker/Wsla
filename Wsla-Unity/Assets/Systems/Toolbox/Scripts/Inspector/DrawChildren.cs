using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace Toolbox
{
    /// <summary>
    /// Draws nested elements without a foldout, perfect for arrays
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class DrawChildrenAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    /// <summary>
    /// Draws nested elements without a foldout, perfect for arrays
    /// </summary>
    [CustomPropertyDrawer(typeof(DrawChildrenAttribute))]
    public class ChildrenPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = 0f;

            var count = 0;

            foreach (var child in property.IterateChildren())
            {
                height += EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;

                count += 1;
            }

            return height;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            rect = rect.ZeroIndent();

            EditorGUI.BeginProperty(rect, label, property);

            foreach (var child in property.IterateChildren())
            {
                var height = EditorGUI.GetPropertyHeight(property, true);
                rect = rect.SliceVertical(height, out var area);
                EditorGUI.PropertyField(area, property, true);

                rect = rect.SliceVertical(EditorGUIUtility.standardVerticalSpacing);
            }

            EditorGUI.EndProperty();
        }
    }
#endif
}