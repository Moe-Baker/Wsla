using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace Toolbox
{
    /// <summary>
    /// An attribute to place on ScriptableObject fields on Monobehaviours to enable them to be created and saved to both scene and disk,
    /// simmilar to the Godot resource system.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class SharedScriptableObjectAttribute : PropertyAttribute
    {
#if UNITY_EDITOR
        public SharedScriptableObjectOptions Options { get; }
#endif

        public SharedScriptableObjectAttribute(SharedScriptableObjectOptions options = SharedScriptableObjectOptions.Default)
        {
#if UNITY_EDITOR
            this.Options = options;
#endif
        }
    }

    [Flags]
    public enum SharedScriptableObjectOptions
    {
        None = 0,

        Default = None,

        AllowDerived = 1 << 0,

        All = ~0,
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SharedScriptableObjectAttribute))]
    public class SharedScriptableObjectDrawer : PropertyDrawer
    {
        static Lazy<GUIContent> PopupContent = new Lazy<GUIContent>(() => EditorGUIUtility.IconContent("d__Popup@2x"));

        void GetOptions(out SharedScriptableObjectOptions options)
        {
            if (attribute is SharedScriptableObjectAttribute shared)
            {
                options = shared.Options;
                return;
            }

            options = SharedScriptableObjectOptions.Default;
        }
        bool GetTarget(SerializedProperty property, out ScriptableObject target)
        {
            if (property.serializedObject.targetObject is ScriptableObject)
            {
                target = null;
                return false;
            }

            if (property.objectReferenceValue == null)
            {
                target = null;
                return true;
            }

            if (property.objectReferenceValue is not ScriptableObject)
            {
                target = null;
                return false;
            }

            target = property.objectReferenceValue as ScriptableObject;
            return true;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (GetTarget(property, out var target) == false)
                return EditorGUIUtility.singleLineHeight;

            if (property.hasMultipleDifferentValues)
                return EditorGUIUtility.singleLineHeight;

            var height = 0f;

            //Foldout
            height += EditorGUIUtility.singleLineHeight;

            if (property.isExpanded == false)
                return height;

            //Space
            height += EditorGUIUtility.standardVerticalSpacing;

            //Field
            height += EditorGUIUtility.singleLineHeight;
            //Space
            height += EditorGUIUtility.standardVerticalSpacing;

            if (target == null)
                return height;

            //Sub-Drawer
            var sub = SubDrawer.Cache.Retrieve(target);
            height += sub.GetHeight();

            return height;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);

            Draw(rect, property, label);

            EditorGUI.EndProperty();
        }
        void Draw(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUIUtility.hierarchyMode = true;

            rect = rect.ZeroIndent();

            if (GetTarget(property, out var target) == false)
            {
                EditorGUI.HelpBox(rect, $"The {nameof(SharedScriptableObjectAttribute)} is Only Valid on ScriptableObjects on Monobehaviours", MessageType.Error);
                return;
            }
            if (property.hasMultipleDifferentValues)
            {
                EditorGUI.HelpBox(rect, $"The {nameof(SharedScriptableObjectAttribute)} Can't Edit Multiple Values", MessageType.Warning);
                return;
            }

            GetOptions(out var options);

            //Foldout
            {
                rect = rect.SliceLine(out var area);

                property.isExpanded = EditorGUI.Foldout(area, property.isExpanded, label, true);

                //Button
                {

                }
            }

            if (property.isExpanded == false)
                return;

            rect = rect.SliceFoldoutIndent();
            rect = rect.SliceStandardSpace();

            //Field + Context
            {
                rect = rect.SliceLine(out var area);

                //Field
                {
                    area = area.SliceHorizontal(area.width - area.height - EditorGUIUtility.standardVerticalSpacing, out var span);

                    var content = new GUIContent("Field");
                    EditorGUI.PropertyField(span, property, content);
                }

                area = area.SliceHorizontal(EditorGUIUtility.standardVerticalSpacing);

                //Context
                {
                    if (GUI.Button(area, GUIContent.none))
                        DisplayContextMenu(property, target, options);

                    EditorGUI.LabelField(area, PopupContent.Value);
                }
            }

            if (target == null)
                return;

            rect = rect.SliceStandardSpace();

            EditorGUIUtility.hierarchyMode = false;

            var sub = SubDrawer.Cache.Retrieve(target);
            sub.Draw(rect);
        }

        void DisplayContextMenu(SerializedProperty property, ScriptableObject target, SharedScriptableObjectOptions options)
        {
            var menu = new GenericMenu();

            var type = property.GetFieldManagedType();

            //Reset
            if (target != null)
            {
                Add("Reset", () =>
                {
                    target.Reset();
                });
            }

            //Create New
            {
                if (options.HasFlag(SharedScriptableObjectOptions.AllowDerived))
                {
                    var derived = TypeCache.GetTypesDerivedFrom(type);

                    Register(FormatPath(type), type);

                    foreach (var item in derived)
                        Register(FormatPath(item), item);

                    static string FormatPath(Type type) => $"Create New/{type.FullName}";
                }
                else
                {
                    Register("Create New", type);
                }

                bool Register(string path, Type type)
                {
                    if (type.CanBeConstructed() == false)
                        return false;

                    Add(path, () =>
                    {
                        target = ScriptableObject.CreateInstance(type);

                        target.name = $"{ObjectNames.NicifyVariableName(target.GetType().Name)} {target.GetInstanceID()}";

                        property.objectReferenceValue = target;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    return true;
                }
            }

            //Make Unique
            if (target != null)
            {
                Add("Make Unique", () =>
                {
                    target = ScriptableObject.Instantiate(target);

                    target.name = $"{ObjectNames.NicifyVariableName(target.GetType().Name)} {target.GetInstanceID()}";

                    property.objectReferenceValue = target;
                    property.serializedObject.ApplyModifiedProperties();

                });
            }

            //Clear Field
            if (target != null)
            {
                Add("Clear Field", () =>
                {
                    target = default;
                    property.objectReferenceValue = target;
                    property.serializedObject.ApplyModifiedProperties();

                });
            }

            //Save to File
            if (EditorUtility.IsPersistent(target) == false)
            {
                Add("Save to File", () =>
                {
                    var path = EditorUtility.SaveFilePanelInProject("Save Scriptable Object", target.name, "asset", "Save Scriptable Object");

                    if (string.IsNullOrEmpty(path))
                        return;

                    AssetDatabase.CreateAsset(target, path);
                });
            }

            void Add(string title, GenericMenu.MenuFunction callback) => menu.AddItem(new GUIContent(title), false, callback);

            menu.ShowAsContext();
        }

        public class SubDrawer
        {
            public ScriptableObject Target { get; }
            public SerializedObject SerializedObject { get; }

            (List<SerializedProperty> Properties, List<float> Heights) Children;

            void PrepareHeights()
            {
                Children.Heights.Clear();

                foreach (var property in Children.Properties)
                {
                    var modifier = EditorGUI.GetPropertyHeight(property, true);
                    Children.Heights.Add(modifier);
                }
            }

            public float GetHeight()
            {
                var height = 0f;

                PrepareHeights();

                foreach (var modifier in Children.Heights)
                {
                    height += modifier;
                    height += EditorGUIUtility.standardVerticalSpacing;
                }

                return height;
            }

            public void Draw(Rect rect)
            {
                SerializedObject.UpdateIfRequiredOrScript();

                if (Children.Properties.Count > Children.Heights.Count)
                    PrepareHeights();

                for (int i = 0; i < Children.Properties.Count; i++)
                {
                    var property = Children.Properties[i];
                    var height = Children.Heights[i];

                    rect = rect.SliceVertical(height, out var area);

                    EditorGUI.PropertyField(area, property, true);

                    rect = rect.SliceVertical(EditorGUIUtility.standardVerticalSpacing);
                }

                SerializedObject.ApplyModifiedProperties();
            }

            public SubDrawer(ScriptableObject target)
            {
                this.Target = target;

                SerializedObject = new SerializedObject(target);

                Children.Properties = new List<SerializedProperty>();

                foreach (var child in SerializedObject.IterateChildren())
                {
                    if (CheckIgnoreProperty(child))
                        continue;

                    var clone = child.Copy();
                    Children.Properties.Add(clone);
                }

                Children.Heights = new(Children.Properties.Count);
            }

            static bool CheckIgnoreProperty(SerializedProperty property)
            {
                if (property.propertyPath == "m_ObjectHideFlags")
                    return true;

                if (property.propertyPath == "m_Script")
                    return true;

                return false;
            }

            public static class Cache
            {
                static Dictionary<ScriptableObject, SubDrawer> Dictionary;

                public static SubDrawer Retrieve(ScriptableObject target)
                {
                    if (Dictionary.TryGetValue(target, out var drawer))
                        return drawer;

                    drawer = new SubDrawer(target);
                    Dictionary[target] = drawer;

                    return drawer;
                }

                static Cache()
                {
                    Dictionary = new Dictionary<ScriptableObject, SubDrawer>();
                }
            }
        }
    }
#endif
}