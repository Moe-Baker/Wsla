using System;
using System.Text;

using UnityEngine;

namespace Toolbox
{
    public static class RichTextMarker
    {
        public static class Explicit
        {
            public static string Bold(object target) => $"<b>{target}</b>";

            public static string Italic(object target) => $"<i>{target}</i>";

            public static string Size(object target, int value) => $"<size={value}>{target}</size>";

            public static string Colorize(object target, ColorSurrogate color) => $"<color=#{color}>{target}</color>";

            public static string Style(object target, bool bold = false, bool italic = false, int? size = null, ColorSurrogate? color = null)
            {
                var text = target.ToString();

                if (bold)
                    text = Bold(text);

                if (italic)
                    text = Italic(text);

                if (size != null)
                    text = Size(text, size.Value);

                if (color != null)
                    text = Colorize(text, color.Value);

                return text;
            }
        }

        public static class Builder
        {
            #region Bold
            public static void BeginBold(StringBuilder builder) => builder.Append("<b>");
            public static void EndBold(StringBuilder builder) => builder.Append("</b>");

            public static BoldBlock Bold(StringBuilder builder) => new BoldBlock(builder);
            public struct BoldBlock : IDisposable
            {
                StringBuilder builder;

                public BoldBlock(StringBuilder builder)
                {
                    this.builder = builder;

                    BeginBold(builder);
                }

                public void Dispose()
                {
                    EndBold(builder);
                }
            }
            #endregion

            #region Italic
            public static void BeginItalic(StringBuilder builder) => builder.Append("<i>");
            public static void EndItalic(StringBuilder builder) => builder.Append("</i>");

            public static ItalicBlock Italic(StringBuilder builder) => new ItalicBlock(builder);
            public struct ItalicBlock : IDisposable
            {
                StringBuilder builder;

                public ItalicBlock(StringBuilder builder)
                {
                    this.builder = builder;

                    BeginItalic(builder);
                }

                public void Dispose()
                {
                    EndItalic(builder);
                }
            }
            #endregion

            #region 
            public static void BeginSize(StringBuilder builder, ReadOnlySpan<char> size)
            {
                builder.Append("<size=");
                builder.Append(size);
                builder.Append(">");
            }
            public static void EndSize(StringBuilder builder) => builder.Append("</size>");

            public static SizeBlock Size(StringBuilder builder, ReadOnlySpan<char> size) => new SizeBlock(builder, size);
            public struct SizeBlock : IDisposable
            {
                StringBuilder builder;

                public SizeBlock(StringBuilder builder, ReadOnlySpan<char> Size)
                {
                    this.builder = builder;

                    BeginSize(builder, Size);
                }

                public void Dispose()
                {
                    EndSize(builder);
                }
            }
            #endregion

            #region Color
            public static void BeginColor(StringBuilder builder, ColorSurrogate color)
            {
                builder.Append("<color=");
                builder.Append(color.Span);
                builder.Append(">");
            }
            public static void EndColor(StringBuilder builder)
            {
                builder.Append("</color>");
            }

            public static ColorBlock Color(StringBuilder builder, ColorSurrogate color) => new ColorBlock(builder, color);
            public struct ColorBlock : IDisposable
            {
                StringBuilder builder;

                public ColorBlock(StringBuilder builder, ColorSurrogate color)
                {
                    this.builder = builder;

                    BeginColor(builder, color);
                }

                public void Dispose()
                {
                    EndColor(builder);
                }
            }
            #endregion
        }

        public static class ValueBuilder
        {
            public static void BeginBold(ref SpanStringBuilder builder) => builder.Append("<b>");
            public static void EndBold(ref SpanStringBuilder builder) => builder.Append("</b>");

            public static void BeginItalic(ref SpanStringBuilder builder) => builder.Append("<i>");
            public static void EndItalic(ref SpanStringBuilder builder) => builder.Append("</i>");

            public static void BeginSize(ref SpanStringBuilder builder, ReadOnlySpan<char> size)
            {
                builder.Append("<size=");
                builder.Append(size);
                builder.Append(">");
            }
            public static void EndSize(ref SpanStringBuilder builder) => builder.Append("</size>");

            public static void BeginColor(ref SpanStringBuilder builder, ColorSurrogate color)
            {
                builder.Append("<color=");
                builder.Append(color.Span);
                builder.Append(">");
            }
            public static void EndColor(ref SpanStringBuilder builder)
            {
                builder.Append("</color>");
            }
        }

        public unsafe struct ColorSurrogate
        {
            public const int Size = 9;

            fixed char Buffer[Size];

            public Span<char> Span
            {
                get
                {
                    fixed (char* character = Buffer)
                    {
                        return new Span<char>(character, Size);
                    }
                }
            }

            public override string ToString() => Span.ToString();

            public ColorSurrogate(ReadOnlySpan<char> characters)
            {
                if (characters.Length != Size)
                    throw new InvalidOperationException($"Characters Span Must be of Size {Size}");

                characters.CopyTo(Span);
            }

            public static implicit operator ColorSurrogate(string text) => new ColorSurrogate(text);
            public static implicit operator ColorSurrogate(ReadOnlySpan<char> characters) => new ColorSurrogate(characters);

            public static Span<char> ColorToHex(Color color, Span<char> characters)
            {
                if (characters.Length < Size)
                    throw new InvalidOperationException($"Characters Buffer Too Small");

                characters[0] = '#';

                Write(color.r, characters.Slice(1, 2));
                Write(color.g, characters.Slice(3, 2));
                Write(color.b, characters.Slice(5, 2));
                Write(color.a, characters.Slice(7, 2));

                return characters.Slice(0, Size);

                static void Write(float component, Span<char> span)
                {
                    var value = Mathf.Clamp(Mathf.RoundToInt(component * 255), 0, 255);

                    if (value.TryFormat(span, out var written, format: "X2") == false)
                        throw new InvalidOperationException($"Span Size Too Small");
                }
            }

            public static implicit operator ColorSurrogate(Color color)
            {
                var characters = ColorToHex(color, stackalloc char[Size]);
                return new ColorSurrogate(characters);
            }
        }
    }
}