using System;

using UnityEngine;

using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Toolbox
{
    [Serializable]
    public unsafe struct SerializedGuid : IEquatable<SerializedGuid>
    {
        [SerializeField]
        fixed byte Binary[BinarySize];

        public const int BinarySize = 16;

        public Guid Value
        {
            get
            {
                fixed (byte* ptr = Binary)
                {
                    var span = new Span<byte>(ptr, BinarySize);
                    return new Guid(span);
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < BinarySize; i++)
                {
                    if (Binary[i] is not 0)
                        return false;
                }

                return true;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is SerializedGuid other)
                return Equals(other);

            return false;
        }
        public bool Equals(SerializedGuid other) => other.Value.Equals(Value);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public SerializedGuid(Guid value)
        {
            Span<byte> buffer = stackalloc byte[BinarySize];

            if (value.TryWriteBytes(buffer) == false)
                throw new NotImplementedException($"Invalid Buffer Size, This is Impossible");

            fixed (byte* source = buffer)
            fixed (byte* destination = Binary)
            {
                Buffer.MemoryCopy(source, destination, BinarySize, BinarySize);
            }
        }

        public static implicit operator SerializedGuid(Guid value) => new SerializedGuid(value);

        public static bool operator ==(SerializedGuid left, SerializedGuid right) => left.Equals(right);
        public static bool operator !=(SerializedGuid left, SerializedGuid right) => !left.Equals(right);

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(SerializedGuid))]
        public class Drawer : PropertyDrawer
        {

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                rect = rect.ZeroIndent();

                EditorGUI.BeginProperty(rect, label, property);

                rect = EditorGUI.PrefixLabel(rect, label);

                var guid = Utility.ReadValue(property);
                var content = Utility.GetDisplayText(guid);
                EditorGUI.SelectableLabel(rect, content);

                EditorGUI.EndProperty();
            }

            public static class Utility
            {
                static Dictionary<Guid, string> DisplayTextCache;

                public const int StringSize = 40;

                public static Guid ReadValue(SerializedProperty property)
                {
                    var binary = property.FindPropertyRelative(nameof(SerializedGuid.Binary));

                    Span<byte> buffer = stackalloc byte[BinarySize];
                    for (int i = 0; i < buffer.Length; i++)
                        buffer[i] = (byte)binary.GetFixedBufferElementAtIndex(i).intValue;

                    return new Guid(buffer);
                }
                public static void SetValue(SerializedProperty property, Guid value)
                {
                    var binary = property.FindPropertyRelative(nameof(SerializedGuid.Binary));

                    Span<byte> buffer = stackalloc byte[BinarySize];
                    if (value.TryWriteBytes(buffer) == false)
                        throw new NotImplementedException($"Invalid Buffer Size, This is Impossible");

                    for (int i = 0; i < buffer.Length; i++)
                        binary.GetFixedBufferElementAtIndex(i).intValue = buffer[i];
                }

                public static string GetDisplayText(Guid value)
                {
                    if (DisplayTextCache.TryGetValue(value, out var text))
                        return text;

                    Span<char> buffer = stackalloc char[StringSize];

                    if (value.TryFormat(buffer, out var written) == false)
                        throw new NotImplementedException($"Invalid Buffer Size, This is Impossible");

                    text = new string(buffer);
                    DisplayTextCache[value] = text;

                    return text;
                }

                static Utility()
                {
                    DisplayTextCache = new();
                }
            }
        }
#endif
    }
}