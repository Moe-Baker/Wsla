using System;

using Toolbox;


#if UNITY_EDITOR
using UnityEditor;

using UnityEngine;
#endif

namespace Wsla.Unity
{
    [Serializable]
    public struct NetworkChannelField
    {
        [field: SerializeField]
        public byte Value { get; private set; }

        public NetworkChannelField(byte Value)
        {
            this.Value = Value;
        }

        public static implicit operator byte(NetworkChannelField field) => field.Value;
        public static implicit operator NetworkChannelField(byte value) => new NetworkChannelField(value);

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(NetworkChannelField))]
        class Drawer : PropertyDrawer
        {
            NetworkAPI API => NetworkAPI.Instance;

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                if (API == null)
                {
                    EditorGUI.HelpBox(rect, $"No NetworkAPI Asset Found", MessageType.Error);
                    return;
                }

                var value = property.FindBackingFieldRelative(nameof(NetworkChannelField.Value));

                rect = EditorGUI.PrefixLabel(rect, label);

                if (EditorGUI.DropdownButton(rect, IndexToContent((byte)value.intValue), FocusType.Keyboard))
                    DisplayDropdown(value);
            }

            void DisplayDropdown(SerializedProperty value)
            {
                var menu = new GenericMenu();

                menu.allowDuplicateNames = true;

                for (byte i = 0; i < API.Channels.Names.Length; i++)
                {
                    var channel = i;
                    var content = IndexToContent(i);

                    menu.AddItem(content, value.intValue == channel, x =>
                    {
                        value.intValue = channel;
                        value.serializedObject.ApplyModifiedProperties();
                    }, i);
                }

                if (API.Channels.Names.Length > 0)
                    menu.AddSeparator("");

                menu.AddItem(new GUIContent("Add Channel..."), false, () =>
                {
                    ProjectWindowUtil.ShowCreatedAsset(API);
                    Selection.activeObject = API;
                });

                menu.ShowAsContext();
            }

            GUIContent IndexToContent(byte value)
            {
                if (API.Channels.TryGetName(value, out var name))
                    return new GUIContent(name);

                return DefaultNames[value];
            }

            static GUIContent[] DefaultNames;
            static Drawer()
            {
                DefaultNames = new GUIContent[Constants.ChannelCount];

                for (int i = 0; i < DefaultNames.Length; i++)
                {
                    var text = $"Channel {i}";
                    DefaultNames[i] = new GUIContent(text);
                }
            }
        }
#endif
    }
}