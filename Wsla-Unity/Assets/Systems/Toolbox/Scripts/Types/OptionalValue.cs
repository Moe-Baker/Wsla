using System;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Toolbox
{
    public interface IOptionalValue<T>
    {
        public bool Enabled { get; }
        public T Value { get; }
    }

    [Serializable]
    public struct OptionalValue<T> : IOptionalValue<T>
    {
        [field: SerializeField]
        public bool Enabled { get; private set; }

        [field: SerializeField]
        public T Value { get; private set; }

        public T Evaluate(T fallback = default) => Enabled ? Value : fallback;

        public OptionalValue(bool enabled, T value)
        {
            this.Enabled = enabled;
            this.Value = value;
        }
        public OptionalValue(T value) : this(true, value) { }
        public OptionalValue(bool enabled) : this(enabled, default) { }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class OptionalValueStyleAttribute : PropertyAttribute
    {
#if UNITY_EDITOR
        public OptionalValueStyle Style { get; }
#endif

        public OptionalValueStyleAttribute(OptionalValueStyle style)
        {
#if UNITY_EDITOR
            this.Style = style;
#endif
        }
    }
    public enum OptionalValueStyle
    {
        /// <summary>
        /// The default way to draw an optional value
        /// </summary>
        Default,

        /// <summary>
        /// Draws the optional value in a single line
        /// </summary>
        Inline,
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(IOptionalValue<>), true)]
    [CustomPropertyDrawer(typeof(OptionalValueStyleAttribute), true)]
    class OptionalValueDrawer : PropertyDrawer
    {
        public float ToggleSize => EditorGUIUtility.singleLineHeight;

        void GetStyle(out OptionalValueStyle style)
        {
            if (attribute is OptionalValueStyleAttribute target)
            {
                style = target.Style;
                return;
            }

            style = OptionalValueStyle.Default;
        }

        void GetPropertyMembers(SerializedProperty property, out SerializedProperty enabled, out SerializedProperty value)
        {
            enabled = property.FindBackingFieldRelative(nameof(IOptionalValue<int>.Enabled));
            value = property.FindBackingFieldRelative(nameof(IOptionalValue<int>.Value));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            GetStyle(out var style);

            var height = 0f;

            switch (style)
            {
                case OptionalValueStyle.Default:
                {
                    //Foldout
                    height += EditorGUIUtility.singleLineHeight;

                    //Children
                    if (property.isExpanded)
                    {
                        //Space
                        height += EditorGUIUtility.standardVerticalSpacing;

                        //Toggle
                        height += EditorGUIUtility.singleLineHeight;

                        //Value
                        {
                            GetPropertyMembers(property, out var enabled, out var value);

                            if (enabled.boolValue)
                            {
                                height += EditorGUIUtility.standardVerticalSpacing;
                                height += EditorGUI.GetPropertyHeight(value, true);
                            }
                        }
                    }
                }
                break;

                case OptionalValueStyle.Inline:
                {
                    height += EditorGUIUtility.singleLineHeight;
                }
                break;

                default: throw new NotImplementedException();
            }

            return height;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            rect = rect.ZeroIndent();

            EditorGUI.BeginProperty(rect, label, property);

            GetStyle(out var style);

            switch (style)
            {
                case OptionalValueStyle.Default:
                    DrawDefault(rect, property, label);
                    break;

                case OptionalValueStyle.Inline:
                    DrawInline(rect, property, label);
                    break;

                default: throw new NotImplementedException();
            }

            EditorGUI.EndProperty();
        }

        void DrawDefault(Rect rect, SerializedProperty property, GUIContent label)
        {
            //Foldout
            {
                rect = rect.SliceVertical(EditorGUIUtility.singleLineHeight, out var area);
                property.isExpanded = EditorGUI.Foldout(area, property.isExpanded, label, true);
            }

            if (property.isExpanded == false)
                return;

            rect = rect.SliceFoldoutIndent().SliceVertical(EditorGUIUtility.standardVerticalSpacing);

            GetPropertyMembers(property, out var enabled, out var value);

            //Toggle
            {
                var content = new GUIContent("Enabled");

                rect = rect.SliceVertical(EditorGUIUtility.singleLineHeight, out var area);

                enabled.boolValue = EditorGUI.Toggle(area, content, enabled.boolValue);
            }

            if (enabled.boolValue == false)
                return;

            rect = rect.SliceVertical(EditorGUIUtility.standardVerticalSpacing);

            //Value
            {
                EditorGUI.PropertyField(rect, value, true);
            }
        }
        void DrawInline(Rect rect, SerializedProperty property, GUIContent label)
        {
            GetPropertyMembers(property, out var enabled, out var value);

            //Toggle
            {
                rect = rect.SliceHorizontal(ToggleSize, out var area);
                area.SliceVertical(ToggleSize, out area);

                enabled.boolValue = EditorGUI.Toggle(area, enabled.boolValue);
            }

            rect = rect.SliceHorizontal(EditorGUIUtility.standardVerticalSpacing);

            //Label
            {
                rect = EditorGUI.PrefixLabel(rect, label);
            }

            //Slice back the toggle size
            rect = rect.SliceHorizontal(-ToggleSize + -EditorGUIUtility.standardVerticalSpacing);

            //Value
            if (enabled.boolValue)
            {
                EditorGUI.PropertyField(rect, value, GUIContent.none, true);
            }
            else
            {
                EditorGUI.LabelField(rect, "None");
            }
        }
    }
#endif
}