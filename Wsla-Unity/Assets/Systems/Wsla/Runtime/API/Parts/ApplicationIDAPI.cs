using System;

using UnityEngine;
using Toolbox;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wsla.Unity
{
    partial class NetworkAPI
    {
        [Serializable]
        public class ApplicationIDProperty
        {
            [field: SerializeField]
            public bool Override { get; private set; }

            [SerializeField]
            string Manual;

            public const int MaxLength = 20;

            public FixedString<FS20> Value { get; private set; }

            internal void Initialize()
            {
                if (Override)
                    Value = Manual;
                else
                    Value = EnsureLength(Application.productName);
            }

            static string EnsureLength(string value)
            {
                if (value.Length > MaxLength)
                    value = value.Substring(0, MaxLength);

                return value;
            }

#if UNITY_EDITOR
            [CustomPropertyDrawer(typeof(ApplicationIDProperty))]
            class Drawer : PropertyDrawer
            {
                public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
                {
                    return EditorGUIUtility.singleLineHeight;
                }

                public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
                {
                    var Override = property.FindBackingFieldRelative(nameof(ApplicationIDProperty.Override));

                    //Draw Checkbox
                    {
                        rect = rect.SliceHorizontal(EditorGUIUtility.singleLineHeight, out var area);
                        rect = rect.SliceHorizontal(EditorGUIUtility.standardVerticalSpacing);

                        Override.boolValue = EditorGUI.Toggle(area, Override.boolValue);
                    }

                    EditorGUIUtility.labelWidth -= EditorGUIUtility.singleLineHeight;

                    if (Override.boolValue)
                    {
                        //Label
                        {
                            rect = rect.SliceHorizontal(EditorGUIUtility.labelWidth, out var area);
                            EditorGUI.LabelField(area, label);
                        }

                        //Field
                        {
                            var Manual = property.FindPropertyRelative(nameof(ApplicationIDProperty.Manual));

                            Manual.stringValue = EditorGUI.TextField(rect, Manual.stringValue);
                        }
                    }
                    else
                    {
                        var value = EnsureLength(Application.productName);
                        EditorGUI.LabelField(rect, label, new GUIContent(value));
                    }
                }
            }
#endif
        }
    }
}