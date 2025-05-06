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
    public class SyncedAssetsAPI : NetworkAPI.Property
    {
        [field: SerializeField]
        public bool Auto { get; private set; } = true;

        [field: SerializeField]
        public ScriptableObject[] Collection { get; private set; }

        Dictionary<ScriptableObject, NetworkEntityResource> Dictionary;

        public bool TryGet(ScriptableObject asset, out NetworkEntityResource id) => Dictionary.TryGetValue(asset, out id);
        public bool TryGet(NetworkEntityResource id, out ScriptableObject asset)
        {
            var index = id.Value;

            if (index >= Collection.Length || index < 0)
            {
                asset = default;
                return false;
            }

            asset = Collection[index];
            return true;
        }

        public override void Set(NetworkAPI value)
        {
            base.Set(value);

#if UNITY_EDITOR
            Collect();
#endif

            Dictionary = new Dictionary<ScriptableObject, NetworkEntityResource>(Collection.Length);

            for (ushort i = 0; i < Collection.Length; i++)
            {
                var asset = Collection[i];

                var id = new NetworkEntityResource(i);

                if (Dictionary.TryAdd(asset, id) is false)
                    throw new InvalidOperationException($"Duplicate Asset ({asset}) Found in SyncedAssets");
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

            Collection = AssetDatabase.FindAssets("t:ScriptableObject")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                .Where(x => x is ISyncedAsset asset && asset.IncludeInSync)
                .ToArray();
        }

        [CustomPropertyDrawer(typeof(SyncedAssetsAPI))]
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