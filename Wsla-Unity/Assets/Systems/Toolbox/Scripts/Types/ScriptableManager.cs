using System;

using UnityEditor;

using UnityEngine;

namespace Toolbox
{
    public abstract class ScriptableManager : ScriptableObject
    {
        /// <summary>
        /// Managers with lower values will get executed first
        /// </summary>
        public virtual int ExecutionOrder => 0;

        /// <summary>
        /// A flag that determines wether this manager will be executed in editor and runtime
        /// </summary>
        public virtual ExecutionModeSelection ExecutionMode => ExecutionModeSelection.Runtime;
        [Flags]
        public enum ExecutionModeSelection
        {
            Editor = 1 << 1,
            Runtime = 1 << 2,

            All = ~0,
        }

        protected abstract void Init();

        static class Runtime
        {
#if UNITY_EDITOR
            [InitializeOnLoadMethod]
            static void OnEditorLoad()
            {
                //This case will be handled by OnRuntimeLoad
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                Init(ExecutionModeSelection.Editor);
            }
#endif

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void OnRuntimeLoad() => Init(ExecutionModeSelection.Runtime);

            static void Init(ExecutionModeSelection mode)
            {
                var managers = Resources.LoadAll<ScriptableManager>("");

                Array.Sort(managers, (x, y) => GeneralUtility.Compare(x.ExecutionOrder, y.ExecutionOrder));

                for (int i = 0; i < managers.Length; i++)
                {
                    if (managers[i].ExecutionMode.HasFlag(mode) == false)
                        continue;

                    try
                    {
                        managers[i].Init();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            }
        }
    }

    public abstract class ScriptableManager<T> : ScriptableManager
        where T : ScriptableManager<T>
    {
        public static T Instance { get; private set; }

        protected override void Init()
        {
            if (Instance != null)
                throw new Exception($"Duplicate Instances of Scriptable Manager ({typeof(T)}) Found, Both ({Instance}) & ({this})");

            Instance = (T)this;

#if UNITY_EDITOR
            //Clear static instance to support instant play mode
            Application.quitting += () =>
            {
                Instance = default;
            };
#endif
        }
    }
}