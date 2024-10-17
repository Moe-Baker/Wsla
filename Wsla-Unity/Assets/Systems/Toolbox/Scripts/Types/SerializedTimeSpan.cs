using System;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Toolbox
{
    [Serializable]
    public struct SerializedTimeSpan
    {
        [field: SerializeField]
        public double Value { get; private set; }

        [field: SerializeField]
        public SerializedTimeSpanUnit Units { get; private set; }

        public TimeSpan Span
        {
            get
            {
                switch (Units)
                {
                    case SerializedTimeSpanUnit.Milliseconds:
                        return TimeSpan.FromMilliseconds(Value);

                    case SerializedTimeSpanUnit.Seconds:
                        return TimeSpan.FromSeconds(Value);

                    case SerializedTimeSpanUnit.Minutes:
                        return TimeSpan.FromMinutes(Value);

                    case SerializedTimeSpanUnit.Hours:
                        return TimeSpan.FromMilliseconds(Value);

                    case SerializedTimeSpanUnit.Days:
                        return TimeSpan.FromDays(Value);

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public SerializedTimeSpan(double duration, SerializedTimeSpanUnit units)
        {
            this.Units = units;
            this.Value = duration;
        }

        public static implicit operator TimeSpan(SerializedTimeSpan target) => target.Span;
        public static implicit operator SerializedTimeSpan(TimeSpan target) => FromTimeSpan(target);

        public static SerializedTimeSpan FromTimeSpan(TimeSpan value)
        {
            var milliseconds = value.TotalMilliseconds;

            var seconds = value.TotalSeconds;
            if (seconds < 1)
                return FromMilliseconds(milliseconds);

            var minutes = value.TotalMinutes;
            if (minutes < 1)
                return FromSeconds(seconds);

            var hours = value.TotalHours;
            if (hours < 1)
                return FromMinutes(minutes);

            var days = value.TotalDays;
            if (days < 1)
                return FromHours(hours);

            return FromDays(days);
        }

        public static SerializedTimeSpan FromMilliseconds(double value) => new(value, SerializedTimeSpanUnit.Milliseconds);
        public static SerializedTimeSpan FromSeconds(double value) => new(value, SerializedTimeSpanUnit.Seconds);
        public static SerializedTimeSpan FromMinutes(double value) => new(value, SerializedTimeSpanUnit.Minutes);
        public static SerializedTimeSpan FromHours(double value) => new(value, SerializedTimeSpanUnit.Hours);
        public static SerializedTimeSpan FromDays(double value) => new(value, SerializedTimeSpanUnit.Days);

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(SerializedTimeSpan))]
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

                //Label
                {
                    rect = EditorGUI.PrefixLabel(rect, label);
                }

                //Units
                {
                    var units = property.FindBackingFieldRelative(nameof(SerializedTimeSpan.Units));

                    rect = rect.SliceHorizontal(rect.width / 2, out var area);

                    EditorGUI.PropertyField(area, units, GUIContent.none);
                }

                rect = rect.SliceHorizontal(EditorGUIUtility.standardVerticalSpacing);

                //Duration
                {
                    var units = property.FindBackingFieldRelative(nameof(SerializedTimeSpan.Value));

                    EditorGUI.PropertyField(rect, units, GUIContent.none);
                }

                EditorGUI.EndProperty();
            }
        }
#endif
    }

    public enum SerializedTimeSpanUnit
    {
        Milliseconds = 0,
        Seconds = 1,
        Minutes = 2,
        Hours = 3,
        Days = 4,
    }
}