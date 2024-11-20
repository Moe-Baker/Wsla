using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
#endif

namespace Toolbox
{
    public interface IPreCache
    {
#if UNITY_EDITOR
        /// <summary>
        /// A method where you can cache all those calls you were making in Awake and Start
        /// [Warning: will be called after Awake but before Start on Scene Objects]
        /// </summary>
        void PreCache();
#endif
    }

#if UNITY_EDITOR
    public static class PreCache
    {
        public class Utility
        {
            static class Cache
            {
                public static List<GameObject> Roots;

                public static List<IPreCache> Contracts;

                static Cache()
                {
                    Roots = new(20);
                    Contracts = new(100);
                }
            }

            public static bool InvokeSceneAsset(SceneAsset asset)
            {
                using var handle = SceneRetrieveHandle.From(asset);

                return InvokeScene(handle.Scene, handle.WasLoaded ? ExecutionOptions.Serialize : ExecutionOptions.SetDirty);
            }
            struct SceneRetrieveHandle : IDisposable
            {
                public Scene Scene { get; }
                public bool WasLoaded { get; }

                public void Dispose()
                {
                    if (WasLoaded == false)
                        return;

                    EditorSceneManager.CloseScene(Scene, true);
                }

                public SceneRetrieveHandle(string path)
                {
                    Scene = EditorSceneManager.GetSceneByPath(path);

                    if (Scene.isLoaded)
                    {
                        WasLoaded = false;
                        return;
                    }

                    Scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    WasLoaded = true;
                }

                public static SceneRetrieveHandle From(SceneAsset asset)
                {
                    var path = AssetDatabase.GetAssetPath(asset);
                    return new SceneRetrieveHandle(path);
                }
            }

            public static bool InvokeScene(Scene scene, ExecutionOptions options)
            {
                scene.GetRootGameObjects(Cache.Roots);

                if (Cache.Roots.Count == 0)
                    return false;

                var changed = false;

                foreach (var root in Cache.Roots)
                {
                    root.GetComponentsInChildren(true, Cache.Contracts);

                    foreach (var contract in Cache.Contracts)
                    {
                        if (options.HasFlag(ExecutionOptions.SetDirty))
                            EditorUtility.SetDirty(contract as Object);

                        changed = true;
                        contract.PreCache();
                    }
                }

                if (changed == false)
                    return false;

                if (options.HasFlag(ExecutionOptions.Serialize))
                    EditorSceneManager.SaveScene(scene);

                return false;
            }

            public static bool InvokeGameObject(GameObject gameObject, ExecutionOptions options)
            {
                gameObject.GetComponentsInChildren(true, Cache.Contracts);

                if (Cache.Contracts.Count == 0)
                    return false;

                foreach (var contract in Cache.Contracts)
                {
                    if (options.HasFlag(ExecutionOptions.SetDirty))
                        EditorUtility.SetDirty(contract as Object);

                    contract.PreCache();
                }

                if (options.HasFlag(ExecutionOptions.Serialize))
                    AssetDatabase.SaveAssetIfDirty(gameObject);

                return true;
            }

            public static bool InvokeScriptableObject(ScriptableObject asset, ExecutionOptions options)
            {
                if (asset is not IPreCache contract)
                    return false;

                contract.PreCache();

                if (options.HasFlag(ExecutionOptions.SetDirty))
                    EditorUtility.SetDirty(asset);

                if (options.HasFlag(ExecutionOptions.Serialize))
                    AssetDatabase.SaveAssetIfDirty(asset);

                return true;
            }
        }

        [Flags]
        public enum ExecutionOptions
        {
            None = 0,

            /// <summary>
            /// Sets the target as dirty
            /// </summary>
            SetDirty = 1 << 1,

            /// <summary>
            /// Sets the target as dirty and saves to disk
            /// </summary>
            Serialize = SetDirty | 1 << 2,
        }

        public class EditorHooks
        {
            [InitializeOnLoadMethod]
            static void OnLoad()
            {
                SceneHierarchyHooks.addItemsToGameObjectContextMenu += GameObjectContextMenu;
                SceneHierarchyHooks.addItemsToSceneHeaderContextMenu += SceneContextMenu;
            }

            static void SceneContextMenu(GenericMenu menu, Scene scene)
            {
                menu.AddItem(new GUIContent("PreCache"), false, () =>
                {
                    Utility.InvokeScene(scene, ExecutionOptions.SetDirty);
                });
            }

            static void GameObjectContextMenu(GenericMenu menu, GameObject selection)
            {
                menu.AddItem(new GUIContent("PreCache"), false, () =>
                {
                    Utility.InvokeGameObject(selection, ExecutionOptions.SetDirty);
                });
            }

            const string AssetContextMenuPath = "Assets/PreCache";
            [MenuItem(AssetContextMenuPath, validate = false)]
            static void AssetContextMenuAction()
            {
                var filters = SelectionMode.Assets | SelectionMode.DeepAssets;
                var options = ExecutionOptions.SetDirty;

                foreach (var asset in Selection.GetFiltered<ScriptableObject>(filters))
                    Utility.InvokeScriptableObject(asset, options);

                foreach (var prefab in Selection.GetFiltered<GameObject>(filters))
                    Utility.InvokeGameObject(prefab, options);

                foreach (var scene in Selection.GetFiltered<SceneAsset>(filters))
                    Utility.InvokeSceneAsset(scene);
            }
            [MenuItem(AssetContextMenuPath, validate = true)]
            static bool AssetContextMenuValidate()
            {
                return true;
            }
        }

        public class SceneProcessor : IProcessSceneWithReport
        {
            public int callbackOrder => -1;

            public void OnProcessScene(Scene scene, BuildReport report) => Utility.InvokeScene(scene, ExecutionOptions.None);
        }

        public class AssetProcessor
        {
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void OnLoad() => Invoke(ExecutionOptions.None);

            static void Invoke(ExecutionOptions options)
            {
                //Prefabs
                {
                    var guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets" });

                    var contracts = new List<IPreCache>(10);

                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                        Utility.InvokeGameObject(prefab, options);
                    }
                }

                //ScriptableObjects
                {
                    var guids = AssetDatabase.FindAssets("t:ScriptableObject", new string[] { "Assets" });

                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                        Utility.InvokeScriptableObject(asset, options);
                    }
                }
            }

            class BuildPreProcessor : IPreprocessBuildWithReport
            {
                public int callbackOrder => -1;

                public void OnPreprocessBuild(BuildReport report) => Invoke(ExecutionOptions.Serialize);
            }
        }
    }
#endif
}