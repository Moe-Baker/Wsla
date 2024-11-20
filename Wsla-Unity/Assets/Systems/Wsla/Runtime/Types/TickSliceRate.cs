using System;

using UnityEngine;
using Toolbox;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wsla.Unity
{
    [Serializable]
    public struct TickSliceRate
    {
        [field: SerializeField, Range(120, 1)]
        public byte Value { get; private set; }

        public double Timestep => API.Tick.FixedTimeStep / Value;

        static NetworkAPI API => NetworkAPI.Instance;

        public TickSliceRate(byte value)
        {
            this.Value = value;
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(TickSliceRate))]
        class Drawer : PropertyDrawer
        {
            NetworkAPI API => NetworkAPI.Instance;

            Lazy<GUIStyle> FrequencyLabelStyle = new(() =>
            {
                return new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            });

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                if (API == null)
                {
                    EditorGUI.HelpBox(rect, $"No NetworkAPI Asset Found", MessageType.Error);
                    return;
                }

                var Value = property.FindBackingFieldRelative(nameof(TickSliceRate.Value));

                //Draw Field
                {
                    rect = rect.SliceHorizontal(rect.width - 60, out var area);
                    EditorGUI.PropertyField(area, Value, label);
                }

                rect = rect.SliceHorizontal(10);

                //Draw Frequency
                {
                    EditorGUI.LabelField(rect, $"{API.Tick.Rate / 1f / Value.intValue}Hz", FrequencyLabelStyle.Value);
                }
            }
        }
#endif
    }
}