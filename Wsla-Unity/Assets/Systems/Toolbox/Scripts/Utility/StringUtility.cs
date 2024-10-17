using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Toolbox
{
    public static class StringUtility
    {
        public static ViewSplitNumerator SplitView(this string input, ReadOnlySpan<char> seperators)
        {
            return new ViewSplitNumerator(input, seperators);
        }
        public ref struct ViewSplitNumerator
        {
            string Input;
            ReadOnlySpan<char> Seperators;

            int Start;
            int End;
            public StringView Current { get; private set; }

            public bool MoveNext()
            {
                while (End <= Input.Length)
                {
                    if (End >= Input.Length || Contains(Seperators, Input[End]))
                    {
                        var view = new StringView(Input, Start, End - Start);

                        End += 1;
                        Start = End;

                        if (StringView.IsEmpty(view) == false)
                        {
                            Current = view;
                            return true;
                        }

                        continue;
                    }

                    End += 1;
                }

                return false;
            }

            bool Contains<T>(ReadOnlySpan<T> span, T element)
                where T : IEquatable<T>
            {
                for (int i = 0; i < span.Length; i++)
                    if (span[i].Equals(element))
                        return true;

                return false;
            }

            public ViewSplitNumerator GetEnumerator() => this;
            public ViewSplitNumerator(string input, ReadOnlySpan<char> seperators)
            {
                this.Input = input;
                this.Seperators = seperators;

                Current = default;
                Start = End = 0;
            }
        }

        public static SpanSplitNumerator SplitSpan(this ReadOnlySpan<char> input, ReadOnlySpan<char> seperators)
        {
            return new SpanSplitNumerator(input, seperators);
        }
        public ref struct SpanSplitNumerator
        {
            ReadOnlySpan<char> Input;
            ReadOnlySpan<char> Seperators;

            int Start;
            int End;
            public ReadOnlySpan<char> Current { get; private set; }

            public bool MoveNext()
            {
                while (End <= Input.Length)
                {
                    if (End >= Input.Length || Contains(Seperators, Input[End]))
                    {
                        var slice = Input.Slice(Start, End - Start);

                        End += 1;
                        Start = End;

                        if (slice.IsWhiteSpace() == false)
                        {
                            Current = slice;
                            return true;
                        }

                        continue;
                    }

                    End += 1;
                }

                return false;
            }

            bool Contains<T>(ReadOnlySpan<T> span, T element)
                where T : IEquatable<T>
            {
                for (int i = 0; i < span.Length; i++)
                    if (span[i].Equals(element))
                        return true;

                return false;
            }

            public SpanSplitNumerator GetEnumerator() => this;
            public SpanSplitNumerator(ReadOnlySpan<char> input, ReadOnlySpan<char> seperators)
            {
                this.Input = input;
                this.Seperators = seperators;

                Current = default;
                Start = End = 0;
            }
        }

        public static bool Equals(this char left, char right, StringComparison comparison)
        {
            switch (comparison)
            {
                case StringComparison.CurrentCultureIgnoreCase:
                case StringComparison.InvariantCultureIgnoreCase:
                    return char.ToLowerInvariant(left) == char.ToLowerInvariant(right);

                case StringComparison.CurrentCulture:
                case StringComparison.InvariantCulture:
                case StringComparison.Ordinal:
                    return left.Equals(right);

                case StringComparison.OrdinalIgnoreCase:
                    return char.ToLower(left) == char.ToLower(right);

                default: throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Reads and then clears the string builder
        /// </summary>
        /// <returns>The contents of the builder</returns>
        public static string Flush(this StringBuilder builder)
        {
            var text = builder.ToString();

            builder.Clear();

            return text;
        }
    }

    public struct StringView : IEquatable<StringView>
    {
        public string Source { get; }
        public int Start { get; }
        public int Length { get; }

        public readonly char this[Index index] => Source[Start + index.GetOffset(Length)];

        public readonly ReadOnlySpan<char> Span => Source.AsSpan().Slice(Start, Length);

        public override string ToString() => new string(Span);

        public override bool Equals(object obj)
        {
            if (obj is StringView other)
                return Equals(other);

            return false;
        }
        public bool Equals(StringView other) => Span.SequenceEqual(other.Span);

        public override int GetHashCode() => FNVHash.Compute(Span);

        public StringView(string source, int start, int length)
        {
            Unsafe.SkipInit(out this);

            this.Source = source;
            this.Start = start;
            this.Length = length;
        }

        public static bool IsEmpty(StringView view)
        {
            for (int i = 0; i < view.Length; i++)
                if (view[i] is not ' ')
                    return false;

            return true;
        }

        public static class Comparers
        {
            public class Default : IEqualityComparer<StringView>
            {
                public bool Equals(StringView x, StringView y) => x.Equals(y);

                public int GetHashCode(StringView obj) => obj.GetHashCode();

                public static Default Instance { get; } = new Default();
            }

            public class IgnoreCase : IEqualityComparer<StringView>
            {
                public bool Equals(StringView x, StringView y)
                {
                    return MemoryExtensions.Equals(x.Span, y.Span, StringComparison.OrdinalIgnoreCase);
                }

                public int GetHashCode(StringView obj)
                {
                    return FNVHash.Compute(obj.Span, ignoreCase: true);
                }

                public static IgnoreCase Instance { get; } = new IgnoreCase();
            }
        }
    }

    public static class FNVHash
    {
        // http://isthe.com/chongo/tech/comp/fnv/
        public const uint FNV_PRIME = 16777619;
        public const uint FNV_OFFSET_BASIS = 2166136261;

        public static int Compute(ReadOnlySpan<char> characters, bool ignoreCase = false)
        {
            var hash = FNV_OFFSET_BASIS;

            for (var i = 0; i < characters.Length; i++)
            {
                byte octet;

                if (ignoreCase)
                    octet = (byte)char.ToLower(characters[i]);
                else
                    octet = (byte)characters[i];

                hash = hash * FNV_PRIME;
                hash = hash ^ octet;
            }

            return Unsafe.As<uint, int>(ref hash);
        }
    }
}