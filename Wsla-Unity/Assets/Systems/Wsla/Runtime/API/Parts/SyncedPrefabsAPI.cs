using System;
using System.Collections.Generic;

using Toolbox;

using UnityEngine;
using UnityEngine.UIElements;

using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

namespace Wsla.Unity
{
    [Serializable]
    public class SyncedPrefabsAPI : NetworkAPI.Property
    {
        [field: SerializeField]
        public bool Auto { get; private set; } = true;

        [field: SerializeField, PrefabOnly]
        public GameObject[] Collection { get; private set; }

        Dictionary<GameObject, NetworkResourceID> Dictionary;

        public bool TryGet(GameObject prefab, out NetworkResourceID id) => Dictionary.TryGetValue(prefab, out id);
        public bool TryGet(NetworkResourceID id, out GameObject prefab)
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

            Collect();

            Dictionary = new Dictionary<GameObject, NetworkResourceID>(Collection.Length);

            for (ushort i = 0; i < Collection.Length; i++)
            {
                var prefab = Collection[i];

                var id = new NetworkResourceID(i);

                if (Dictionary.TryAdd(prefab, id) is false)
                    throw new InvalidOperationException($"Duplicate Prefab ({prefab}) Found in SyncedAssets");
            }
        }

        internal void PreCache()
        {
            Collect();
        }

#if UNITY_EDITOR
        internal void Collect()
        {
            if (Auto is false) return;

            Collection = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(x => x.TryGetComponent<NetworkEntity>(out _))
                .ToArray();
        }

        [CustomPropertyDrawer(typeof(SyncedPrefabsAPI))]
        class Drawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI(SerializedProperty property)
            {
                return new CustomElement(property);
            }

            class CustomElement : Foldout
            {
                SerializedProperty Property;

                Toggle Auto;
                PropertyField Collection;

                public CustomElement(SerializedProperty Property)
                {
                    this.Property = Property;

                    text = Property.displayName;

                    //Auto
                    {
                        var field = Property.FindBackingFieldRelative(nameof(SyncedAssetsAPI.Auto));

                        Auto = new Toggle("Auto");
                        Auto.BindProperty(field);

                        Add(Auto);
                    }

                    //Collection
                    {
                        var field = Property.FindBackingFieldRelative(nameof(SyncedAssetsAPI.Collection));

                        Collection = new PropertyField(field);

                        Add(Collection);
                    }

                    Auto.RegisterValueChangedCallback(x => AutoToggleCallback(x.newValue));
                    AutoToggleCallback(Auto.value);
                }

                void AutoToggleCallback(bool value)
                {
                    Collection.enabledSelf = !value;
                }
            }
        }
#endif
    }
}