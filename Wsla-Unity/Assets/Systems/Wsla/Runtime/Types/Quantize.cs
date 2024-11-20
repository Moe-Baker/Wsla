using System;

using UnityEngine;

using System.Runtime.CompilerServices;
using Wsla.Serialization;
using Toolbox;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wsla.Unity
{
    public static unsafe class Quantize
    {
        public static class Float
        {
            public static void Serialize(INetworkStream writer, float value, FloatQuantizationParameters parameter)
            {
                Serialize(writer, value, parameter.Min, parameter.Max, parameter.Bits);
            }
            public static void Serialize(INetworkStream writer, float value, float min, float max, byte bits)
            {
                var quantization = Compress(value, min, max, bits);
                var bytes = BitsToBytes(bits);
                Write(writer, quantization, bytes);
            }

            public static float Deserialize(INetworkStream reader, FloatQuantizationParameters parameter)
            {
                return Deserialize(reader, parameter.Min, parameter.Max, parameter.Bits);
            }
            public static float Deserialize(INetworkStream reader, float min, float max, byte bits)
            {
                var bytes = BitsToBytes(bits);
                var quantization = Read<ulong>(reader, bytes);

                return Decompress(quantization, min, max, bits);
            }

            public static ulong Compress(float value, float min, float max, byte bits)
            {
                var rate = InverseLerp(min, max, value);
                var multiplier = MaxForBits(bits);

                var quantization = (ulong)Math.Round(rate * multiplier);
                return quantization;
            }
            public static float Decompress(ulong quantization, float min, float max, byte bits)
            {
                var multiplier = MaxForBits(bits);
                var rate = (quantization & multiplier) / 1.0d / multiplier;

                var value = Lerp(min, max, rate);
                return (float)value;
            }

            static double Lerp(double a, double b, double t) => (a + (b - a) * t);
            static double InverseLerp(double a, double b, double value) => (value - a) / (b - a);

            public static byte BitsForPrecision(float min, float max, float precision)
            {
                var range = Mathf.Abs(max - min);
                return BitsForPrecision(range, precision);
            }
            public static byte BitsForPrecision(float range, float precision)
            {
                return (byte)(Math.Log(range / precision, 2) + 1);
            }
        }

        public static class Integer
        {
            public static void Serialize(INetworkStream writer, int value, IntegerQuantizationParameters parameters)
            {
                Serialize(writer, value, parameters.Min, parameters.Max, parameters.Bits);
            }
            public static void Serialize(INetworkStream writer, int value, int min, int max)
            {
                var bits = BitsForRange(min, max);
                Serialize(writer, value, min, max, bits);
            }
            public static void Serialize(INetworkStream writer, int value, int min, int max, byte bits)
            {
                var bytes = BitsToBytes(bits);

                var quantization = Compress(value, min, max);
                Write(writer, quantization, bytes);
            }

            public static int Deserialize(INetworkStream reader, IntegerQuantizationParameters parameters)
            {
                return Deserialize(reader, parameters.Min, parameters.Max);
            }
            public static int Deserialize(INetworkStream reader, int min, int max)
            {
                var bits = BitsForRange(min, max);
                return Deserialize(reader, min, max, bits);
            }
            public static int Deserialize(INetworkStream reader, int min, int max, byte bits)
            {
                var bytes = BitsToBytes(bits);
                var quantization = Read<uint>(reader, bytes);

                return Decompress(quantization, min, max);
            }

            public static uint Compress(int value, int min, int max)
            {
                var delta = max - min;
                return (uint)(delta - max + value);
            }
            public static int Decompress(uint quantization, int min, int max)
            {
                return (int)(min + quantization);
            }

            public static byte BitsForRange(int min, int max) => BitsForNumber(max - min);
        }

        public static class Flag
        {
            public static void Serialize(INetworkStream writer, ulong value, byte bits)
            {
                var bytes = BitsToBytes(bits);
                Write(writer, value, bytes);
            }
            public static ulong Deserialize(INetworkStream reader, byte bits)
            {
                var bytes = BitsToBytes(bits);
                return Read<ulong>(reader, bytes);
            }
        }

        public static class Rotation
        {
            public const float Range = 0.72f;
            public const float Min = -Range;
            public const float Max = +Range;

            const uint IndexMask = (1u << 0) | (1u << 1);
            const byte ComponentBitCount = 10;

            public const byte MaxBytes = 4;
            public const byte MaxBits = 4 * 8;

            public static void Serialize(INetworkStream writer, Quaternion target)
            {
                var quantization = Compress(target);
                NetworkSerializer.WriteValue(in quantization, writer);
            }
            public static Quaternion Deserialize(INetworkStream reader)
            {
                NetworkSerializer.ReadValue(reader, out uint quantization);
                return Decompress(quantization);
            }

            #region Compress
            public static uint Compress(Quaternion target)
            {
                ParseComponents(target, out var index, out var a, out var b, out var c);

                uint quantization = 0;
                byte shift = 0;

                //Write Index (2 bits)
                quantization |= index;
                shift += 2;

                WriteComponent(ref quantization, ref shift, a);
                WriteComponent(ref quantization, ref shift, b);
                WriteComponent(ref quantization, ref shift, c);

                return quantization;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void ParseComponents(Quaternion target, out byte index, out float a, out float b, out float c)
            {
                float max = float.MinValue;
                int sign = 1;
                index = 0;

                for (byte i = 0; i < 4; i++)
                {
                    var value = Mathf.Abs(target[i]);

                    if (value > max)
                    {
                        index = i;
                        max = value;
                        sign = (target[i] < 0) ? -1 : 1;
                    }
                }

                switch (index)
                {
                    //Ignore X
                    case 0:
                    {
                        a = target.y;
                        b = target.z;
                        c = target.w;
                    }
                    break;

                    //Ignore Y
                    case 1:
                    {
                        a = target.x;
                        b = target.z;
                        c = target.w;
                    }
                    break;

                    //Ignore Z
                    case 2:
                    {
                        a = target.x;
                        b = target.y;
                        c = target.w;
                    }
                    break;

                    //Ignore W
                    case 3:
                    {
                        a = target.x;
                        b = target.y;
                        c = target.z;
                    }
                    break;

                    default:
                        throw new NotImplementedException();
                }

                a *= sign;
                b *= sign;
                c *= sign;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void WriteComponent(ref uint quantization, ref byte shift, float component)
            {
                var step = (uint)Float.Compress(component, Min, Max, ComponentBitCount);
                quantization |= (step << shift);
                shift += ComponentBitCount;
            }
            #endregion

            #region Decompress
            public static Quaternion Decompress(uint quantization)
            {
                byte shift = 0;

                var index = (quantization & IndexMask);
                shift += 2;

                var a = ReadComponent(quantization, ref shift);
                var b = ReadComponent(quantization, ref shift);
                var c = ReadComponent(quantization, ref shift);
                var d = Mathf.Sqrt(1f - ((a * a) + (b * b) + (c * c)));

                switch (index)
                {
                    case 0:
                        return new Quaternion(d, a, b, c);

                    case 1:
                        return new Quaternion(a, d, b, c);

                    case 2:
                        return new Quaternion(a, b, d, c);

                    case 3:
                        return new Quaternion(a, b, c, d);

                    default:
                        throw new NotImplementedException();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static float ReadComponent(uint quantization, ref byte shift)
            {
                quantization >>= shift;
                var component = Float.Decompress(quantization, Min, Max, ComponentBitCount);
                shift += ComponentBitCount;
                return component;
            }
            #endregion
        }

        public static class Angle
        {
            public const float Min = 0f;
            public const float Max = 360f;

            public const byte MaxBytes = 2;
            public const byte MaxBits = MaxBytes * 8;

            public static void Serialize(INetworkStream writer, float angle)
            {
                var quantization = Compress(angle);
                NetworkSerializer.WriteValue(in quantization, writer);
            }
            public static float Deserialize(INetworkStream reader)
            {
                NetworkSerializer.ReadValue(reader, out ushort quantization);
                return Decompress(quantization);
            }

            public static ushort Compress(float angle)
            {
                angle = Clamp(angle);
                return (ushort)Float.Compress(angle, Min, Max, MaxBits);
            }
            public static float Decompress(ushort quantization)
            {
                return Float.Decompress(quantization, Min, Max, MaxBits);
            }

            public static float Clamp(float angle)
            {
                angle %= 360;

                if (angle < 0)
                    angle += 360;

                return angle;
            }
        }

        public static byte BitsForNumber(double value)
        {
            return (byte)(Math.Log(value, 2) + 1);
        }

        public static ulong MaxForBits(byte bits) => (1ul << bits) - 1ul;

        public static byte BitsToBytes(byte bits) => (byte)((bits + 7) / 8);

        static void Write<T>(INetworkStream stream, T value, int bytes)
            where T : unmanaged
        {
            var buffer = stream.PopSpan(bytes);

            fixed (byte* destination = buffer)
            {
                Buffer.MemoryCopy(&value, destination, bytes, bytes);
            }
        }
        static T Read<T>(INetworkStream stream, int bytes)
            where T : unmanaged
        {
            var value = default(T);

            var buffer = stream.PopSpan(bytes);

            fixed (byte* source = buffer)
            {
                Buffer.MemoryCopy(source, &value, bytes, bytes);
            }

            return value;
        }
    }

    [Serializable]
    public struct FloatQuantizationParameters : ISerializationCallbackReceiver
    {
        [field: SerializeField]
        public bool Range { get; private set; }

        [field: SerializeField]
        public float Min { get; private set; }

        [field: SerializeField]
        public float Max { get; private set; }

        [field: SerializeField]
        public float Precision { get; private set; }

        [field: SerializeField]
        public byte Bits { get; private set; }

        public void OnBeforeSerialize()
        {
            Bits = Quantize.Float.BitsForPrecision(Min, Max, Precision);
        }
        public void OnAfterDeserialize() { }

        public FloatQuantizationParameters(float range, float precision)
        {
            this.Range = true;
            this.Min = -range;
            this.Max = +range;
            this.Precision = precision;

            Bits = Quantize.Float.BitsForPrecision(Min, Max, Precision);
        }
        public FloatQuantizationParameters(float min, float max, float precision)
        {
            this.Range = false;
            this.Min = min;
            this.Max = max;
            this.Precision = precision;

            Bits = Quantize.Float.BitsForPrecision(Min, Max, Precision);
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(FloatQuantizationParameters))]
        public class Drawer : PropertyDrawer
        {
            static void Init(SerializedProperty property, out SerializedProperty range, out SerializedProperty min, out SerializedProperty max, out SerializedProperty precision, out SerializedProperty bits)
            {
                range = property.FindBackingFieldRelative("Range");
                min = property.FindBackingFieldRelative("Min");
                max = property.FindBackingFieldRelative("Max");
                precision = property.FindBackingFieldRelative("Precision");
                bits = property.FindBackingFieldRelative("Bits");
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                Init(property, out var range, out var min, out var max, out var precision, out var bits);

                var height = 0f;

                height += EditorGUIUtility.singleLineHeight;

                if (property.isExpanded == false)
                    return height;

                //Change
                height += EditorGUIUtility.singleLineHeight;
                height += EditorGUIUtility.standardVerticalSpacing;

                if (range.boolValue)
                {
                    height += EditorGUIUtility.singleLineHeight;
                    height += EditorGUIUtility.standardVerticalSpacing;
                }
                else
                {
                    height += EditorGUIUtility.singleLineHeight;
                    height += EditorGUIUtility.standardVerticalSpacing;

                    height += EditorGUIUtility.singleLineHeight;
                    height += EditorGUIUtility.standardVerticalSpacing;
                }

                //Precision
                height += EditorGUIUtility.singleLineHeight;
                height += EditorGUIUtility.standardVerticalSpacing;

                //Bits
                height += EditorGUIUtility.singleLineHeight;

                return height;
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                Init(property, out var range, out var min, out var max, out var precision, out var bits);

                //Foldout
                {
                    rect = rect.SliceLine(out var area);
                    property.isExpanded = EditorGUI.Foldout(area, property.isExpanded, label, true);
                }

                if (property.isExpanded == false) return;

                rect = rect.SliceIndent();

                //Range
                {
                    rect = rect.SliceLine(out var area);
                    rect = rect.SliceStandardSpace();

                    range.boolValue = EditorGUI.Toggle(area, "Range", range.boolValue);
                }

                if (range.boolValue)
                {
                    rect = rect.SliceLine(out var area);
                    rect = rect.SliceStandardSpace();

                    max.floatValue = EditorGUI.FloatField(area, "Area", max.floatValue);
                    min.floatValue = -max.floatValue;
                }
                else
                {
                    //Min
                    {
                        rect = rect.SliceLine(out var area);
                        rect = rect.SliceStandardSpace();

                        EditorGUI.PropertyField(area, min);
                    }

                    //Max
                    {
                        rect = rect.SliceLine(out var area);
                        rect = rect.SliceStandardSpace();

                        EditorGUI.PropertyField(area, max);
                    }
                }

                //Precision
                {
                    rect = rect.SliceLine(out var area);
                    rect = rect.SliceStandardSpace();

                    EditorGUI.PropertyField(area, precision);
                }

                //Bits
                {
                    rect = rect.SliceLine(out var area);
                    rect = rect.SliceStandardSpace();

                    GUI.enabled = false;
                    EditorGUI.LabelField(area, $"{bits.intValue} Bits -> {Quantize.BitsToBytes((byte)bits.intValue)} Bytes");
                    GUI.enabled = true;
                }
            }
        }
#endif
    }

    [Serializable]
    public struct IntegerQuantizationParameters : ISerializationCallbackReceiver
    {
        [field: SerializeField]
        public bool Range { get; private set; }

        [field: SerializeField]
        public int Min { get; private set; }

        [field: SerializeField]
        public int Max { get; private set; }

        [field: SerializeField]
        public byte Bits { get; private set; }

        public void OnBeforeSerialize()
        {
            Bits = Quantize.Integer.BitsForRange(Min, Max);
        }
        public void OnAfterDeserialize() { }

        public IntegerQuantizationParameters(int range)
        {
            this.Range = true;
            this.Min = -range;
            this.Max = +range;

            Bits = Quantize.Integer.BitsForRange(Min, Max);
        }
        public IntegerQuantizationParameters(int min, int max)
        {
            this.Range = false;
            this.Min = min;
            this.Max = max;

            Bits = Quantize.Integer.BitsForRange(Min, Max);
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(IntegerQuantizationParameters))]
        public class Drawer : PropertyDrawer
        {
            static void Init(SerializedProperty property, out SerializedProperty range, out SerializedProperty min, out SerializedProperty max, out SerializedProperty bits)
            {
                range = property.FindBackingFieldRelative("Range");
                min = property.FindBackingFieldRelative("Min");
                max = property.FindBackingFieldRelative("Max");
                bits = property.FindBackingFieldRelative("Bits");
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                Init(property, out var range, out var min, out var max, out var bits);

                var height = 0f;

                height += EditorGUIUtility.singleLineHeight;

                if (property.isExpanded == false)
                    return height;

                //Change
                height += EditorGUIUtility.singleLineHeight;
                height += EditorGUIUtility.standardVerticalSpacing;

                if (range.boolValue)
                {
                    height += EditorGUIUtility.singleLineHeight;
                    height += EditorGUIUtility.standardVerticalSpacing;
                }
                else
                {
                    height += EditorGUIUtility.singleLineHeight;
                    height += EditorGUIUtility.standardVerticalSpacing;

                    height += EditorGUIUtility.singleLineHeight;
                    height += EditorGUIUtility.standardVerticalSpacing;
                }

                //Bits
                height += EditorGUIUtility.singleLineHeight;

                return height;
            }

            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                Init(property, out var range, out var min, out var max, out var bits);

                //Foldout
                {
                    rect = rect.SliceLine(out var area);
                    rect = rect.SliceStandardSpace();

                    property.isExpanded = EditorGUI.Foldout(area, property.isExpanded, label, true);
                }

                if (property.isExpanded == false) return;

                rect = rect.SliceIndent();

                //Range
                {
                    rect = rect.SliceLine(out var area);
                    rect = rect.SliceStandardSpace();

                    range.boolValue = EditorGUI.Toggle(area, "Range", range.boolValue);
                }

                if (range.boolValue)
                {
                    rect = rect.SliceLine(out var area);
                    rect = rect.SliceStandardSpace();

                    max.intValue = EditorGUI.IntField(area, "Area", max.intValue);
                    min.intValue = -max.intValue;
                }
                else
                {
                    //Min
                    {
                        rect = rect.SliceLine(out var area);
                        rect = rect.SliceStandardSpace();

                        EditorGUI.PropertyField(area, min);
                    }

                    //Max
                    {
                        rect = rect.SliceLine(out var area);
                        rect = rect.SliceStandardSpace();

                        EditorGUI.PropertyField(area, max);
                    }
                }

                //Bits
                {
                    rect = rect.SliceLine(out var area);
                    rect = rect.SliceStandardSpace();

                    GUI.enabled = false;
                    EditorGUI.LabelField(area, $"{bits.intValue} Bits -> {Quantize.BitsToBytes((byte)bits.intValue)} Bytes");
                    GUI.enabled = true;
                }
            }
        }
#endif
    }
}