using System;

using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
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
        /// Draws the optional value in a foldout
        /// </summary>
        Foldout,

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
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            GetStyle(out var style);

            return style switch
            {
                OptionalValueStyle.Foldout => new FoldoutStyle(property),
                OptionalValueStyle.Inline => new InlineStyle(property),

                _ => throw new NotImplementedException()
            };
        }

        void GetStyle(out OptionalValueStyle style)
        {
            if (attribute is OptionalValueStyleAttribute target)
            {
                style = target.Style;
                return;
            }

            style = OptionalValueStyle.Foldout;
        }

        class FoldoutStyle : Foldout
        {
            Toggle Enabled;
            PropertyField Value;

            public FoldoutStyle(SerializedProperty property)
            {
                this.text = property.displayName;
                this.BindProperty(property);

                //Enabled
                {
                    var member = property.FindBackingFieldRelative(nameof(IOptionalValue<int>.Enabled));
                    Enabled = new Toggle(member.displayName);

                    Enabled.BindProperty(member);

                    Add(Enabled);
                }

                //Value
                {
                    var member = property.FindBackingFieldRelative(nameof(IOptionalValue<int>.Value));
                    Value = new PropertyField(member);

                    Add(Value);
                }

                Enabled.RegisterValueChangedCallback(x => UpdateState());
                UpdateState();
            }

            void UpdateState()
            {
                Value.style.display = Enabled.value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        class InlineStyle : VisualElement
        {
            Toggle Enabled;
            PropertyField Value;

            public InlineStyle(SerializedProperty property)
            {
                style.flexDirection = FlexDirection.Row;

                //Enabled
                {
                    var member = property.FindBackingFieldRelative(nameof(IOptionalValue<int>.Enabled));
                    Enabled = new Toggle();

                    Enabled.BindProperty(member);

                    Enabled.style.paddingRight = 2.5f;

                    Add(Enabled);
                }

                //Value
                {
                    var member = property.FindBackingFieldRelative(nameof(IOptionalValue<int>.Value));
                    Value = new PropertyField(member, property.displayName);

                    Add(Value);
                }

                Enabled.RegisterValueChangedCallback(x => UpdateState());
                UpdateState();
            }

            void UpdateState()
            {
                var field = Value.GetValue<VisualElement>("m_ChildField");
                if (field == null)
                    return;

                var input = field.GetValue<VisualElement>("m_VisualInput");

                input.visible = Enabled.value;
            }
        }
    }
#endif
}