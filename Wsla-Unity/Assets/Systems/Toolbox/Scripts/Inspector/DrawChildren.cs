using System;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

using UnityEngine;
using UnityEngine.UIElements;

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
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new Element(property);
        }

        class Element : VisualElement
        {
            public Element(SerializedProperty property)
            {
                foreach (var child in property.IterateChildren())
                {
                    var field = new PropertyField(child);
                    Add(field);
                }
            }
        }
    }
#endif
}