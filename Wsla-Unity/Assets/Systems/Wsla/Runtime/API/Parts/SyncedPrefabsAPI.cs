using System;
using System.Collections.Generic;

using Toolbox;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wsla.Unity
{
    [Serializable]
    public class SyncedPrefabsAPI : NetworkAPI.Property
    {
        [field: SerializeField, PrefabOnly]
        public GameObject[] Collection { get; private set; }

        Dictionary<GameObject, NetworkEntityResource> Dictionary;

        public bool TryGet(GameObject prefab, out NetworkEntityResource id) => Dictionary.TryGetValue(prefab, out id);
        public bool TryGet(NetworkEntityResource id, out GameObject prefab)
        {
            var index = id.Value;

            if (index >= Collection.Length || index < 0)
            {
                prefab = default;
                return false;
            }

            prefab = Collection[index];
            return true;
        }

        public override void Set(NetworkAPI value)
        {
            base.Set(value);

            Dictionary = new Dictionary<GameObject, NetworkEntityResource>(Collection.Length);

            for (ushort i = 0; i < Collection.Length; i++)
            {
                var prefab = Collection[i];

                var id = new NetworkEntityResource(i);

                if (Dictionary.TryAdd(prefab, id) is false)
                    throw new InvalidOperationException($"Duplicate Prefab ({prefab}) Found in SyncedAssets");
            }
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(SyncedPrefabsAPI))]
        class Drawer : PropertyDrawer
        {
            void Init(SerializedProperty property, out SerializedProperty names)
            {
                names = property.FindBackingFieldRelative(nameof(SyncedPrefabsAPI.Collection));
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                Init(property, out var names);

                return EditorGUI.GetPropertyHeight(names, label, true);
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                Init(property, out var names);

                EditorGUI.PropertyField(rect, names, label, true);
            }
        }
#endif
    }
}