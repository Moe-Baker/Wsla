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
        public struct GameVersionProperty
        {
            [field: SerializeField]
            public bool Override { get; private set; }

            [SerializeField]
            byte Major;

            [SerializeField]
            byte Minor;

            [SerializeField]
            byte Patch;

            public NetworkVersion Value { get; private set; }

            internal GameVersionProperty Initialize()
            {
                if (Override)
                {
                    Value = new NetworkVersion(Major, Minor, Patch);
                }
                else
                {
                    if (NetworkVersion.TryParse(Application.version, out var result) is false)
                        throw new InvalidOperationException($"Can't Convert ({Application.version}) to Unity Version");

                    Value = result;
                }

                return this;
            }

#if UNITY_EDITOR
            [CustomPropertyDrawer(typeof(GameVersionProperty))]
            class Drawer : PropertyDrawer
            {
                static Lazy<GUIStyle> DotLabelStyle = new(() =>
                {
                    return new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                    };
                });

                public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
                {
                    return EditorGUIUtility.singleLineHeight;
                }

                public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
                {
                    var Override = property.FindBackingFieldRelative(nameof(GameVersionProperty.Override));

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
                            DrawInputField(ref rect, property, "Major");
                            DrawDot(ref rect);
                            DrawInputField(ref rect, property, "Minor");
                            DrawDot(ref rect);
                            DrawInputField(ref rect, property, "Patch");

                            static void DrawInputField(ref Rect rect, SerializedProperty property, string name)
                            {
                                var sub = property.FindPropertyRelative(name);

                                rect = rect.SliceHorizontal(30, out var area);

                                sub.intValue = EditorGUI.IntField(area, sub.intValue);
                            }
                            static void DrawDot(ref Rect rect)
                            {
                                rect = rect.SliceHorizontal(6, out var area);

                                EditorGUI.LabelField(area, ".", DotLabelStyle.Value);
                            }
                        }
                    }
                    else
                    {
                        if (NetworkVersion.TryParse(Application.version, out var result))
                            EditorGUI.LabelField(rect, label, new GUIContent(Application.version));
                        else
                            EditorGUI.HelpBox(rect, $"Can't Parse {Application.version} as Network Version", MessageType.Error);
                    }
                }
            }
#endif
        }
    }
}