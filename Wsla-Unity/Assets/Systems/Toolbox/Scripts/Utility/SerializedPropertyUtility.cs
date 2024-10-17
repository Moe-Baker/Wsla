#if UNITY_EDITOR
using System;
using System.Reflection;

using CommunityToolkit.HighPerformance.Buffers;

using UnityEditor;

using UObject = UnityEngine.Object;

namespace Toolbox
{
    public static class SerializedPropertyUtility
    {
        public static StringPool StringPool { get; } = new();

        public static Type GetFieldManagedType(this SerializedProperty property)
        {
            TypeUtility.Evaluate(property, out var field, out var type);
            return type;
        }
        public static class TypeUtility
        {
            public static class Constants
            {
                public const string Assembly = "UnityEditor";
                public const string Namespace = "UnityEditor";
                public const string Class = "ScriptAttributeUtility";
                public const string Method = "GetFieldInfoAndStaticTypeFromProperty";
            }

            static DelegateMethod Delegate;
            public delegate FieldInfo DelegateMethod(SerializedProperty property, out Type type);

            internal static void Evaluate(SerializedProperty property, out FieldInfo field, out Type type)
            {
                field = Delegate(property, out type);
                return;
            }

            static TypeUtility()
            {
                var type = Type.GetType($"{Constants.Namespace}.{Constants.Class}, {Constants.Assembly}");

                var method = type.GetMethod(Constants.Method, BindingFlags.Static | BindingFlags.NonPublic);

                Delegate = method.CreateDelegate(typeof(DelegateMethod)) as DelegateMethod;
            }
        }

        public static DynamicObjectHierarchy GetManagedHierarchy(this SerializedProperty property)
        {
            return GetManagedHierarchy(property, property.serializedObject.targetObject);
        }
        public static DynamicObjectHierarchy GetManagedHierarchy(this SerializedProperty property, UObject root)
        {
            var builder = new SpanStringBuilder(stackalloc char[property.propertyPath.Length]);
            builder.Append(property.propertyPath);
            builder.Replace(".Array.data[", "[");
            var path = builder.ToSpan();

            var hierarchy = DynamicObjectHierarchy.From(root);

            foreach (var slice in path.SplitSpan(stackalloc char[] { '.', '[', ']' }))
            {
                if (int.TryParse(slice, out var index))
                {
                    hierarchy = hierarchy.AddElement(index);
                }
                else
                {
                    var id = StringPool.GetOrAdd(slice);
                    hierarchy = hierarchy.AddField(id);
                }
            }

            return hierarchy;
        }

        public static SerializedProperty FindBackingField(this SerializedObject target, string name)
        {
            name = Toolbox.TypeUtility.FormatBacingFieldName(name);
            return target.FindProperty(name);
        }
        public static SerializedProperty FindBackingFieldRelative(this SerializedProperty property, string name)
        {
            name = Toolbox.TypeUtility.FormatBacingFieldName(name);
            return property.FindPropertyRelative(name);
        }

        public static ObjectChildrenNumerator IterateChildren(this SerializedObject target)
        {
            return new ObjectChildrenNumerator(target);
        }
        public ref struct ObjectChildrenNumerator
        {
            SerializedObject Target;

            bool InitialIteration;

            public SerializedProperty Current { get; private set; }

            public ObjectChildrenNumerator(SerializedObject target)
            {
                this.Target = target;

                Current = target.GetIterator();

                InitialIteration = true;
            }

            public bool MoveNext()
            {
                if (InitialIteration)
                {
                    InitialIteration = false;
                    return Current.Next(true);
                }
                else
                {
                    return Current.NextVisible(false);
                }
            }

            public ObjectChildrenNumerator GetEnumerator() => this;
        }

        public static PropertyChildrenNumerator IterateChildren(this SerializedProperty property)
        {
            return new PropertyChildrenNumerator(property);
        }
        public ref struct PropertyChildrenNumerator
        {
            public SerializedProperty Current { get; private set; }

            bool InitialIteration;
            SerializedProperty EndProperty;

            public PropertyChildrenNumerator(SerializedProperty root)
            {
                InitialIteration = true;
                EndProperty = root.GetEndProperty();

                Current = root;
            }

            public bool MoveNext()
            {
                if (InitialIteration)
                {
                    if (Current.hasVisibleChildren == false)
                        return false;

                    InitialIteration = false;
                    return Current.NextVisible(true);
                }
                else
                {
                    if (Current.NextVisible(false) == false) return false;
                    if (Current.propertyPath == EndProperty.propertyPath) return false;

                    return true;
                }
            }

            public PropertyChildrenNumerator GetEnumerator() => this;
        }
    }
}
#endif