using System;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Toolbox
{
    /// <summary>
    /// An attribute to mark only allowing prefabs when selecting
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class PrefabOnlyAttribute : PropertyAttribute
    {
#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(PrefabOnlyAttribute))]
        public class Drawer : PropertyDrawer
        {
            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                rect = rect.ZeroIndent();

                EditorGUI.BeginProperty(rect, label, property);

                var target = property.objectReferenceValue;
                var type = typeof(GameObject);

                target = EditorGUI.ObjectField(rect, label, target, type, false);

                property.objectReferenceValue = target;

                EditorGUI.EndProperty();
            }
        }
#endif
    }
}