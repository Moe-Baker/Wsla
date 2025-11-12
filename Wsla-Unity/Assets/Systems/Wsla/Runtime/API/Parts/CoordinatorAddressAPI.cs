using System;
using System.Net;
using System.Threading.Tasks;

using UnityEngine;

using Toolbox;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wsla.Unity
{
    partial class NetworkAPI
    {
        [Serializable]
        public class CoordinatorAddressProperty
        {
            [field: SerializeField]
            public string Hostname { get; private set; }

            [field: SerializeField]
            public IPAddress IP { get; private set; }

            internal async Task<WslaResponse<WslaError>> Prepare()
            {
                try
                {
                    if (IPAddress.TryParse(Hostname, out var value) is false)
                    {
                        var collection = await Dns.GetHostAddressesAsync(Hostname);
                        value = collection[0];
                    }

                    NetworkLog.Info($"Coordinator Address: {value}");

                    IP = value;

                    return true;
                }
                catch (Exception ex)
                {
                    return WslaError.From(ex);
                }
            }

#if UNITY_EDITOR
            [CustomPropertyDrawer(typeof(CoordinatorAddressProperty))]
            class Drawer : PropertyDrawer
            {
                public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
                {
                    return EditorGUIUtility.singleLineHeight;
                }

                public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
                {
                    var hostname = property.FindBackingFieldRelative(nameof(CoordinatorAddressProperty.Hostname));

                    EditorGUI.PropertyField(rect, hostname, label);
                }
            }
#endif
        }
    }
}
