using System;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UObject = UnityEngine.Object;

namespace Toolbox
{
    [Serializable]
    public struct SceneReference : ISerializationCallbackReceiver
    {
        [SerializeField]
        UObject Asset;

        [field: SerializeField]
        public string Name { get; private set; }

        [field: SerializeField]
        public string Path { get; private set; }

        [field: SerializeField]
        public int BuildIndex { get; private set; }

        public bool IsInBuild => BuildIndex > -1;

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(Name))
                    return false;

                if (string.IsNullOrEmpty(Path))
                    return false;

                if (IsInBuild == false)
                    return false;

                return true;
            }
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            UpdateInfo();
#endif
        }
        public void OnAfterDeserialize() { }

#if UNITY_EDITOR
        void UpdateInfo()
        {
            var info = GetAssetInfo(Asset);

            Name = info.name;
            Path = info.path;
            BuildIndex = info.buildIndex;
        }

        static (string name, string path, int buildIndex) GetAssetInfo(UObject asset)
        {
            if (asset == null)
                return (string.Empty, string.Empty, -1);

            var name = asset.name;
            var path = AssetDatabase.GetAssetPath(asset);
            var guid = AssetDatabase.GUIDFromAssetPath(path);

            var buildIndex = GetBuildIndex(guid);

            return (name, path, buildIndex);
        }

        static int GetBuildIndex(GUID guid)
        {
            var index = 0;

            var list = EditorBuildSettings.scenes;

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].guid == guid)
                {
                    if (list[i].enabled == false)
                        index = -1;

                    return index;
                }

                if (list[i].enabled)
                    index += 1;
            }

            return -1;
        }

        [CustomPropertyDrawer(typeof(SceneReference))]
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

                var asset = property.FindPropertyRelative(nameof(SceneReference.Asset));

                asset.objectReferenceValue = EditorGUI.ObjectField(rect, label, asset.objectReferenceValue, typeof(SceneAsset), false);

                EditorGUI.EndProperty();
            }
        }
#endif
    }
}