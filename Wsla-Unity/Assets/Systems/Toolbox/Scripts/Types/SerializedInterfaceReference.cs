using System;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UObject = UnityEngine.Object;

namespace Toolbox
{
    [Serializable]
    public struct SerializedInterfaceReference<T>
        where T : class
    {
        [SerializeField]
        internal UObject Context;

        public T Value => Context as T;

        public SerializedInterfaceReference(T value)
        {
            if (value is not UObject context)
                throw new ArgumentException($"Value Must Inherit from {typeof(UObject)}");

            this.Context = value as UObject;
        }

        public static implicit operator T(SerializedInterfaceReference<T> target) => target.Value;
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SerializedInterfaceReference<>), true)]
    class SerializedInterfaceReferenceDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            rect = rect.ZeroIndent();

            EditorGUI.BeginProperty(rect, label, property);

            rect = EditorGUI.PrefixLabel(rect, label);

            var constraint = GetConstraintInterfaceType(property);
            var context = property.FindPropertyRelative(nameof(SerializedInterfaceReference<SerializedInterfaceReferenceDrawer>.Context));

            if (DrawNullField(rect, context, constraint)) { }
            else if (DrawGameObjectField(rect, context, constraint)) { }
            else if (DrawComponentField(rect, context, constraint)) { }
            else if (DrawScriptableObject(rect, context, constraint)) { }
            else context.objectReferenceValue = default;

            EditorGUI.EndProperty();
        }

        bool DrawNullField(Rect rect, SerializedProperty context, Type constraint)
        {
            var asset = context.objectReferenceValue;

            if (asset != null)
                return false;

            asset = EditorGUI.ObjectField(rect, asset, typeof(UObject), true);

            context.objectReferenceValue = asset;

            return true;
        }

        bool DrawGameObjectField(Rect rect, SerializedProperty context, Type constraint)
        {
            var target = context.objectReferenceValue;

            if (target is not GameObject && target is not Component)
                return false;

            Span<Rect> areas = stackalloc Rect[2];

            //Split Rect
            {
                const float Spacing = 5f;

                //Left
                {
                    var width = rect.width * 0.5f;

                    areas[0] = new Rect(rect)
                    {
                        x = rect.x,
                        width = width,
                    };

                    areas[0].xMax -= Spacing;
                }

                //Right
                {
                    var width = rect.width * 0.5f;

                    areas[1] = new Rect(rect)
                    {
                        x = rect.x + width,
                        width = width,
                    };

                    areas[1].xMax += Spacing;
                }
            }

            //Draw GameObject
            {
                target = EditorGUI.ObjectField(areas[0], target, typeof(UObject), true);
            }

            if (GetGameObjectComponentPair(target, out var gameObject, out var component) == false)
            {
                context.objectReferenceValue = target;
                return true;
            }

            var content = ComponentToGUIContent(component);
            if (EditorGUI.DropdownButton(areas[1], content, FocusType.Keyboard))
            {
                var menu = GetComponentSelectionMenu(gameObject, component, constraint, Callback);
                menu.ShowAsContext();

                void Callback(Component value)
                {
                    if (value == null)
                        context.objectReferenceValue = gameObject;
                    else
                        context.objectReferenceValue = value;

                    context.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                if (component)
                {
                    if (IsAssignable(constraint, component))
                        target = component;
                    else
                        target = gameObject;
                }

                context.objectReferenceValue = target;
            }

            return true;
        }
        static bool GetGameObjectComponentPair(UObject target, out GameObject gameObject, out Component component)
        {
            if (target is GameObject)
            {
                gameObject = target as GameObject;
                component = default;
                return true;
            }
            else if (target is Component)
            {
                component = target as Component;
                gameObject = component.gameObject;
                return true;
            }

            gameObject = default;
            component = default;
            return false;
        }
        GenericMenu GetComponentSelectionMenu(GameObject gameObject, UObject current, Type constraint, Action<Component> callback)
        {
            var menu = new GenericMenu();

            var all = gameObject.GetComponents<Component>();

            //Add 'None' Item
            {
                var content = ComponentToGUIContent(default);
                menu.AddItem(content, current == default, Handler, default);
            }

            menu.AddSeparator("");

            foreach (var item in all)
            {
                var content = ComponentToGUIContent(item);

                if (IsAssignable(constraint, item))
                    menu.AddItem(content, item == current, Handler, item);
                else
                    menu.AddDisabledItem(content, item == current);
            }

            void Handler(object data)
            {
                var component = (Component)data;
                callback(component);
            }

            return menu;
        }
        GUIContent ComponentToGUIContent(Component component)
        {
            if (component == null)
                return new GUIContent("None");

            var name = component.GetType().Name;
            name = ObjectNames.NicifyVariableName(name);

            return new GUIContent(name);
        }

        bool DrawComponentField(Rect rect, SerializedProperty context, Type constraint)
        {
            var target = context.objectReferenceValue;

            if (target is not Component)
                return false;

            target = EditorGUI.ObjectField(rect, target, typeof(UObject), true);

            if (IsAssignable(constraint, target) == false)
                if (target is Component component)
                    target = component.gameObject;

            context.objectReferenceValue = target;

            return true;
        }

        bool DrawScriptableObject(Rect rect, SerializedProperty context, Type constraint)
        {
            var asset = context.objectReferenceValue;

            if (asset is not ScriptableObject)
                return false;

            asset = EditorGUI.ObjectField(rect, asset, typeof(UObject), true);

            context.objectReferenceValue = asset;

            return true;
        }

        bool IsAssignable<T>(Type constraint, T target) => constraint.IsAssignableFrom(target.GetType());

        public Type GetConstraintInterfaceType(SerializedProperty property)
        {
            var type = property.GetFieldManagedType();
            return type.GenericTypeArguments[0];
        }
    }
#endif
}