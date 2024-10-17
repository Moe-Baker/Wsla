using System;

using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Toolbox
{
    /// <summary>
    /// A type that can be used to make a variable in Unity Objects to show a button in the inspector
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public struct ButtonField
    {
#if UNITY_EDITOR
        [NonSerialized]
        internal string Title;

        ButtonFieldDelegate<object> Callback;

        internal ButtonFieldOperation Invoke(object target) => Callback(target);
#endif

#if UNITY_EDITOR
        ButtonField(string title, ButtonFieldDelegate<object> callback)
        {
            this.Title = title;
            this.Callback = callback;
        }
#endif

        public static ButtonField Create<TSelf>(ButtonFieldDelegate<TSelf> callback) => Create(default, callback);
        public static ButtonField Create<TSelf>(string name, ButtonFieldDelegate<TSelf> callback)
        {
#if UNITY_EDITOR
            if (callback is not ButtonFieldDelegate<object> surrogate)
            {
                surrogate = target =>
                {
                    if (target is not TSelf self)
                    {
                        Debug.LogError($"Couldn't Convert '{target}' of Type '{target.GetType()}' to '{typeof(TSelf)}'");
                        return ButtonFieldOperation.None;
                    }

                    return callback(self);
                };
            }

            return new ButtonField(name, surrogate);
#else
            return default;
#endif
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ButtonField))]
    class ButtonFieldDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            //Name
            {
                var hierarchy = property.GetManagedHierarchy();

                var self = (ButtonField)hierarchy.Self.Context;

                if (string.IsNullOrEmpty(self.Title) is false)
                    label.text = self.Title;
            }

            if (GUI.Button(rect, label))
            {
                foreach (var target in property.serializedObject.targetObjects)
                {
                    var hierarchy = property.GetManagedHierarchy(target);

                    var self = (ButtonField)hierarchy[^1].Object.Context;
                    var parent = hierarchy[^2].Context;

                    var operation = self.Invoke(parent);

                    switch (operation)
                    {
                        case ButtonFieldOperation.SetDirty:
                            EditorUtility.SetDirty(target);
                            break;

                        case ButtonFieldOperation.Serialize:
                            EditorUtility.SetDirty(target);
                            AssetDatabase.SaveAssetIfDirty(target);
                            break;
                    }
                }
            }
        }
    }
#endif

    public delegate ButtonFieldOperation ButtonFieldDelegate<T>(T self);

    public enum ButtonFieldOperation
    {
        None,

        /// <summary>
        /// Sets the parent object as dirty
        /// </summary>
        SetDirty,

        /// <summary>
        /// Sets the parent object as dirty & saves the object to disk
        /// </summary>
        Serialize
    }
}