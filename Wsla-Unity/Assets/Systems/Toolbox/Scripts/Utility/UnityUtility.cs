using UnityEditor;

using UnityEngine;

namespace Toolbox
{
    public static class UnityUtility
    {
#if UNITY_EDITOR
        /// <summary>
        /// Resets a ScriptableObject to it's initial values [Editor Only]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="target"></param>
        public static void Reset(this ScriptableObject target)
        {
            if (target == null)
                return;

            var type = target.GetType();
            var clean = ScriptableObject.CreateInstance(type);
            clean.name = target.name;

            Undo.RecordObject(target, "Undo Reset");

            EditorUtility.CopySerialized(clean, target);

            ScriptableObject.DestroyImmediate(clean);
        }
#endif
    }
}