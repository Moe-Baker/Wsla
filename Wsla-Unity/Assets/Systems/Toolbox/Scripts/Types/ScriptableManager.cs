using System;
using System.Threading;

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
                ExecutionContext = mode;

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

#if UNITY_EDITOR
                    //Clear static instance to support instant play mode

                    var reference = managers[i];

                    if (mode.HasFlag(ExecutionModeSelection.Editor))
                    {
                        EditorApplication.playModeStateChanged += Callback;
                        void Callback(PlayModeStateChange state)
                        {
                            if (state is not PlayModeStateChange.ExitingEditMode)
                                return;

                            EditorApplication.playModeStateChanged -= Callback;
                            reference.Dispose();
                        }
                    }

                    if (mode.HasFlag(ExecutionModeSelection.Runtime))
                    {
                        Application.quitting += Callback;
                        void Callback()
                        {
                            Application.quitting -= Callback;
                            reference.Dispose();
                        }
                    }
#endif
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
        }

        public event Action OnDispose;
        protected override void Dispose()
        {
            Instance = default;

            OnDispose?.Invoke();
        }
    }
}