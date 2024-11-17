using System;
using System.Data.SqlTypes;

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

        protected abstract void Dispose();

        public static class Runtime
        {
            public static ExecutionModeSelection ExecutionContext { get; private set; }

            public static ScriptableManager[] Instances { get; private set; }

#if UNITY_EDITOR
            [InitializeOnLoadMethod]
            static void OnEditorLoad()
            {
                //This case will be handled by OnRuntimeLoad
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                AssemblyReloadEvents.beforeAssemblyReload += () =>
                {
                    Dispose();
                };

                EditorApplication.playModeStateChanged += state =>
                {
                    switch (state)
                    {
                        case PlayModeStateChange.EnteredEditMode:
                            Init(ExecutionModeSelection.Editor);
                            break;

                        case PlayModeStateChange.EnteredPlayMode:
                            //Handled by OnRuntimeLoad
                            break;

                        case PlayModeStateChange.ExitingPlayMode:
                            Dispose();
                            break;

                        case PlayModeStateChange.ExitingEditMode:
                            Dispose();
                            break;

                    }
                };

                Init(ExecutionModeSelection.Editor);
            }
#endif

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void OnRuntimeLoad() => Init(ExecutionModeSelection.Runtime);

            static void Init(ExecutionModeSelection mode)
            {
                ExecutionContext = mode;

                Instances = Resources.LoadAll<ScriptableManager>("");

                Array.Sort(Instances, (x, y) => GeneralUtility.Compare(x.ExecutionOrder, y.ExecutionOrder));

                for (int i = 0; i < Instances.Length; i++)
                {
                    if (Instances[i].ExecutionMode.HasFlag(mode) == false)
                        continue;

                    try
                    {
                        Instances[i].Init();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            }

            static void Dispose()
            {
                for (int i = 0; i < Instances.Length; i++)
                    Instances[i].Dispose();

                Instances = null;
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
        }

        public event Action OnDispose;
        protected override void Dispose()
        {
            OnDispose?.Invoke();

            OnDispose = default;
            Instance = default;
        }
    }
}