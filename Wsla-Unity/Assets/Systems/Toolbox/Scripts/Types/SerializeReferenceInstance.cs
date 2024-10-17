using System;

using UnityEngine;

using System.Collections.Generic;
using System.Reflection;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Toolbox
{
    public interface ISerializeReferenceInstance<T>
    {
        T Value { get; }
    }

    [Serializable]
    public struct SerializeReferenceInstance<T> : ISerializeReferenceInstance<T>
    {
        [field: SerializeReference]
        public T Value { get; private set; }

        public SerializeReferenceInstance(T value)
        {
            this.Value = value;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class SerializeReferenceInstanceStyleAttribute : PropertyAttribute
    {
#if UNITY_EDITOR
        public SerializeReferenceInstanceStyle Stlye { get; }
#endif

        public SerializeReferenceInstanceStyleAttribute(SerializeReferenceInstanceStyle style)
        {
#if UNITY_EDITOR
            this.Stlye = style;
#endif
        }
    }
    public enum SerializeReferenceInstanceStyle
    {
        /// <summary>
        /// The default waiy to draw
        /// </summary>
        Default,

        /// <summary>
        /// Draw with no Foldout
        /// </summary>
        NoFoldout
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ISerializeReferenceInstance<>), true)]
    [CustomPropertyDrawer(typeof(SerializeReferenceInstanceStyleAttribute), true)]
    public class SerializeReferenceInstanceDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = 0f;

            GetStyle(out var style);

            if (style is not SerializeReferenceInstanceStyle.NoFoldout)
            {
                //Foldout
                height += EditorGUIUtility.singleLineHeight;

                if (property.isExpanded == false)
                    return height;

                //Space
                height += EditorGUIUtility.standardVerticalSpacing;
            }

            //Type Dropdown
            height += EditorGUIUtility.singleLineHeight;

            GetValueSerializedProperty(property, out var value);
            if (value.propertyType is not SerializedPropertyType.ManagedReference)
                return EditorGUIUtility.singleLineHeight;

            if (value.managedReferenceValue is null)
                return height;

            //Space
            height += EditorGUIUtility.standardVerticalSpacing;

            //Value
            height += EditorGUI.GetPropertyHeight(value, label, true);

            return height;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            rect = rect.ZeroIndent();

            EditorGUI.BeginProperty(rect, label, property);

            DrawProperty(rect, property, label);

            EditorGUI.EndProperty();
        }

        void GetValueSerializedProperty(SerializedProperty property, out SerializedProperty value)
        {
            value = property.FindBackingFieldRelative(nameof(SerializeReferenceInstance<SerializeReferenceInstanceDrawer>.Value));
        }
        void GetStyle(out SerializeReferenceInstanceStyle style)
        {
            if (attribute is SerializeReferenceInstanceStyleAttribute target)
            {
                style = target.Stlye;
                return;
            }

            style = SerializeReferenceInstanceStyle.Default;
        }
        void GetConstraintType(SerializedProperty property, out Type constraint)
        {
            var type = property.GetFieldManagedType();

            constraint = ConstraintTypeCache.Fetch(type);
        }

        void DrawProperty(Rect rect, SerializedProperty property, GUIContent label)
        {
            GetValueSerializedProperty(property, out var value);
            if (value.propertyType is not SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(rect, $"Can Only Apply {nameof(SerializeReferenceInstanceDrawer)} to fields Marked with $[{nameof(SerializeReference)}]", MessageType.Error);
                return;
            }

            GetConstraintType(property, out var constraint);

            GetStyle(out var style);

            //Foldout
            if (style is not SerializeReferenceInstanceStyle.NoFoldout)
            {
                rect = rect.SliceVertical(EditorGUIUtility.singleLineHeight, out var area);

                property.isExpanded = EditorGUI.Foldout(area, property.isExpanded, label, true);

                if (property.isExpanded == false)
                    return;

                rect = rect.SliceFoldoutIndent();
            }

            //Draw Button
            {
                rect = rect.SliceVertical(EditorGUIUtility.singleLineHeight, out var area);

                //Label
                {
                    var content = new GUIContent("Type");

                    area = EditorGUI.PrefixLabel(area, content);
                }

                //Dropdown
                {
                    var type = GetValueType(value);

                    var content = TypeToGUIContent(type, false);

                    if (EditorGUI.DropdownButton(area, content, FocusType.Keyboard))
                    {
                        var selection = GetSelection(constraint);

                        var menu = GetGenericMenu(selection, type, Callback);
                        menu.ShowAsContext();

                        void Callback(Type target)
                        {
                            if (target is null)
                                value.managedReferenceValue = default;
                            else
                                value.managedReferenceValue = Activator.CreateInstance(target);

                            value.serializedObject.ApplyModifiedProperties();
                        }
                    }
                }
            }

            if (value.managedReferenceValue is null)
                return;

            rect = rect.SliceVertical(EditorGUIUtility.standardVerticalSpacing);

            //Draw Value
            {
                var content = new GUIContent("Value");

                EditorGUI.PropertyField(rect, value, content, true);
            }
        }

        List<Type> GetSelection(Type constraint)
        {
            var all = TypeCache.GetTypesDerivedFrom(constraint);

            var selection = new List<Type>(all.Count);

            foreach (var type in all)
            {
                if (type.IsAbstract)
                    continue;

                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                    continue;

                selection.Add(type);
            }

            selection.Sort((x, y) => string.Compare(x.FullName, y.FullName));

            return selection;
        }

        GenericMenu GetGenericMenu(IReadOnlyList<Type> list, Type selection, Action<Type> callback)
        {
            var menu = new GenericMenu();

            //Add 'None' Entry
            {
                var content = TypeToGUIContent(default, true);
                menu.AddItem(content, selection == default, Handler, default);
            }

            menu.AddSeparator("");

            foreach (var item in list)
            {
                var content = TypeToGUIContent(item, true);
                menu.AddItem(content, selection == item, Handler, item);
            }

            void Handler(object data)
            {
                var type = (Type)data;

                callback(type);
            }

            return menu;
        }

        Type GetValueType(SerializedProperty property)
        {
            var reference = property.managedReferenceValue;

            return reference?.GetType();
        }

        GUIContent TypeToGUIContent(Type type, bool context)
        {
            if (type == null)
                return new GUIContent("None");

            var data = SerializeReferenceTypeDisplayInfoCache.Retrieve(type);

            return new GUIContent(context ? data.Path : data.Name);
        }

        public static class ConstraintTypeCache
        {
            static Dictionary<Type, Type> Collection;

            public static Type Fetch(Type type)
            {
                if (Collection.TryGetValue(type, out var constraint))
                    return constraint;

                constraint = Get(type);

                Collection[type] = constraint;

                return constraint;
            }

            static Type Get(Type type)
            {
                var interfaces = type.GetInterfaces();

                for (int i = 0; i < interfaces.Length; i++)
                    if (IsInterfaceAssignableFrom(typeof(ISerializeReferenceInstance<>), interfaces[i]))
                        return interfaces[i].GenericTypeArguments[0];

                throw new InvalidOperationException($"Type Doesn't Derive from the {typeof(ISerializeReferenceInstance<>).Name} Interface");
            }

            static bool IsInterfaceAssignableFrom(Type parent, Type child)
            {
                if (parent.IsInterface is false)
                    throw new ArgumentException($"Parent is not an Interface");

                if (child.IsInterface is false)
                    throw new ArgumentException($"Child is not an Interface");

                if (child.IsGenericType == false)
                    return false;

                return child.GetGenericTypeDefinition() == parent;
            }

            static ConstraintTypeCache()
            {
                Collection = new();
            }
        }
    }
#endif

    #region Type Display Info

    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class SerializeReferenceTypeDisplayInfoAttribute : Attribute
    {
#if UNITY_EDITOR
        public string Location { get; }
        public string Name { get; }
#endif

        public SerializeReferenceTypeDisplayInfoAttribute(string name) : this("", name) { }
        public SerializeReferenceTypeDisplayInfoAttribute(string location, string name)
        {
#if UNITY_EDITOR
            this.Location = location;
            this.Name = name;
#endif
        }
    }

#if UNITY_EDITOR
    public static class SerializeReferenceTypeDisplayInfoCache
    {
        static Dictionary<Type, Data> Dictionary;
        public struct Data
        {
            public string Path { get; }
            public string Name { get; }

            public Data(string path, string name)
            {
                this.Path = path;
                this.Name = name;
            }
        }

        public static Data Retrieve(Type type)
        {
            if (Dictionary.TryGetValue(type, out var data))
                return data;

            data = Parse(type);
            Dictionary[type] = data;

            return data;
        }

        static Data Parse(Type type)
        {
            var attribute = type.GetCustomAttribute<SerializeReferenceTypeDisplayInfoAttribute>();

            if (attribute is null)
            {
                var name = type.Name;
                name = ObjectNames.NicifyVariableName(name);

                var path = FormatHeaderString(type.FullName);
                path = ObjectNames.NicifyVariableName(path);

                return new Data(path, name);
            }
            else
            {
                var name = attribute.Name;
                name = ObjectNames.NicifyVariableName(name);

                var path = string.IsNullOrEmpty(attribute.Location) ? attribute.Name : $"{attribute.Location}/{attribute.Name}";
                path = ObjectNames.NicifyVariableName(path);

                return new Data(path, name);
            }
        }

        static string FormatHeaderString(string input)
        {
            var span = input.Length > 1024 ? new char[input.Length] : stackalloc char[input.Length];

            var change = false;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '.' || input[i] == '+')
                {
                    span[i] = '/';
                    change = true;
                }
                else
                {
                    span[i] = input[i];
                }
            }

            if (change == false)
                return input;

            return new string(span);
        }

        static SerializeReferenceTypeDisplayInfoCache()
        {
            Dictionary = new();
        }
    }
#endif

    #endregion
}