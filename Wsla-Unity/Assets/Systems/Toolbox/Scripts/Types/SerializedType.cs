using System;

using UnityEngine;

using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Toolbox
{
    [Serializable]
    public struct SerializedType : ISerializationCallbackReceiver
    {
        [SerializeField]
        string Name;

        [SerializeField]
        string ID;

        public Type Value { get; private set; }

        public void OnBeforeSerialize()
        {
            ID = TypeToID(Value);
            Name = TypeToName(Value);
        }
        public void OnAfterDeserialize()
        {
            Value = IDToType(ID);
        }

        public SerializedType(Type value)
        {
            this.Value = value;

            ID = TypeToID(value);
            Name = TypeToName(value);
        }

        public static implicit operator Type(in SerializedType target) => target.Value;

        public static string TypeToID(Type type)
        {
            if (type is null)
                return string.Empty;

            return type.AssemblyQualifiedName;
        }
        public static Type IDToType(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return Type.GetType(id);
        }

        public static string TypeToName(Type type)
        {
            if (type is null)
                return "None";

            return type.Name;
        }

        [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
        public class ParentSelectionAttribute : PropertyAttribute
        {
#if UNITY_EDITOR
            public Type Parent { get; }
            public ParentBehaviour Behaviour { get; }

            public bool IncludeParent => Behaviour.HasFlag(ParentBehaviour.IncludeSelf);
            public bool IncludeAbstract => Behaviour.HasFlag(ParentBehaviour.IncludeAbstract);

            public bool Check(Type type)
            {
                if (IncludeAbstract == false && type.IsAbstract == true)
                    return false;

                if (IncludeParent == false && type == Parent)
                    return false;

                if (Parent.IsAssignableFrom(type) == false)
                    return false;

                return true;
            }
#endif

            public ParentSelectionAttribute(Type parent, ParentBehaviour behaviour = ParentBehaviour.IncludeSelf)
            {
#if UNITY_EDITOR
                this.Parent = parent;
                this.Behaviour = behaviour;
#endif
            }
        }
        [Flags]
        public enum ParentBehaviour
        {
            None = 0,

            IncludeSelf = 1 << 0,
            IncludeAbstract = 1 << 1,

            All = ~0,
        }

        [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
        public class FilterSelectionAttribute : PropertyAttribute
        {
#if UNITY_EDITOR
            public Type Construct { get; }

            public Predicate<Type> Get()
            {
                var filter = FilterBehaviour.Collection.Get(Construct);
                return filter.Check;
            }
#endif

            public FilterSelectionAttribute(Type construct)
            {
#if UNITY_EDITOR
                this.Construct = construct;
#endif
            }
        }
        public abstract class FilterBehaviour
        {
            public abstract bool Check(Type type);

            protected FilterBehaviour() { }

#if UNITY_EDITOR
            internal static class Collection
            {
                static Dictionary<Type, FilterBehaviour> Dictionary;

                public static FilterBehaviour Get(Type type)
                {
                    if (Dictionary.TryGetValue(type, out var filter))
                        return filter;

                    try
                    {
                        filter = (FilterBehaviour)Activator.CreateInstance(type);
                    }
                    catch (Exception)
                    {
                        throw new ArgumentException($"Can't Create Serialized Type Filter from ({type}), Type Must Inherit from {typeof(FilterBehaviour)} And be Constructable");
                    }

                    Dictionary.Add(type, filter);

                    return filter;
                }

                static Collection()
                {
                    Dictionary = new();
                }
            }
#endif
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(SerializedType))]
        [CustomPropertyDrawer(typeof(ParentSelectionAttribute))]
        [CustomPropertyDrawer(typeof(FilterSelectionAttribute))]
        class Drawer : PropertyDrawer
        {
            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                rect = rect.ZeroIndent();

                EditorGUI.BeginProperty(rect, label, property);

                var id = property.FindPropertyRelative(nameof(SerializedType.ID));
                var type = IDToType(id.stringValue);

                rect = EditorGUI.PrefixLabel(rect, label);

                var content = TypeToGUIContent(type, false);
                if (EditorGUI.DropdownButton(rect, content, FocusType.Keyboard))
                {
                    var selector = GetSelector();

                    var selection = GetSelection(selector);

                    var menu = GetGenericMenu(selection, type, Callback);

                    menu.ShowAsContext();

                    void Callback(Type value)
                    {
                        id.stringValue = TypeToID(value);
                        id.serializedObject.ApplyModifiedProperties();
                    }
                }

                EditorGUI.EndProperty();
            }

            Predicate<Type> GetSelector()
            {
                if (attribute is ParentSelectionAttribute parent)
                    return parent.Check;

                if (attribute is FilterSelectionAttribute filter)
                    return filter.Get();

                return x => true;
            }

            List<Type> GetSelection(Predicate<Type> selector)
            {
                var all = TypeCache.GetTypesDerivedFrom<System.Object>();

                var selection = new List<Type>();

                foreach (var type in all)
                    if (selector(type))
                        selection.Add(type);

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

            GUIContent TypeToGUIContent(Type type, bool context)
            {
                if (type == null)
                    return new GUIContent("None");

                var name = type.FullName;

                if (context)
                    name = FormatHeaderString(name);

                name = ObjectNames.NicifyVariableName(name);

                return new GUIContent(name);
            }

            string FormatHeaderString(string input)
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
        }
#endif
    }
}