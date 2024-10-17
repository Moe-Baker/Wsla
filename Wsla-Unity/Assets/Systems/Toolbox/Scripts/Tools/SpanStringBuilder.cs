using System;
using System.Net;
using System.Numerics;

namespace Toolbox
{
    public ref struct SpanStringBuilder
    {
        Span<char> characters;
        int size;
        int position;

        public int Remaining => size - position;

        #region Append
        public void NewLine() => Append('\n');

        public void Space() => Append(' ');

        public void Append(ReadOnlySpan<char> characters)
        {
            var target = TakeSlice(characters.Length);
            characters.CopyTo(target);
        }

        public void Append(bool target)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(char character)
        {
            CheckLength(1);
            characters[position] = character;
            position += 1;
        }

        public void Append(byte target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }
        public void Append(sbyte target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(short target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }
        public void Append(ushort target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(int target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }
        public void Append(uint target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(long target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }
        public void Append(ulong target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(BigInteger target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(float target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }
        public void Append(double target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }
        public void Append(decimal target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(Guid target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(DateTime target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(DateTimeOffset target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(TimeSpan target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }

        public void Append(IPAddress target)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            position += written;
        }
        #endregion

        public void Increment(int offset)
        {
            if (offset > Remaining)
                throw new ArgumentOutOfRangeException("Cannot Increment Position");

            position += offset;
        }

        #region Slicing
        /// <summary>
        /// Advances the builder by the length and returns the advanced slice
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public Span<char> TakeSlice(int length)
        {
            CheckLength(length);

            var span = characters.Slice(position, length);

            position += length;

            return span;
        }

        /// <summary>
        /// Gets the remaining build space
        /// </summary>
        /// <returns></returns>
        public Span<char> GetRemaining()
        {
            var span = characters.Slice(position, Remaining);

            return span;
        }

        /// <summary>
        /// Validates if the builder can accept the extra length
        /// </summary>
        /// <param name="length"></param>
        /// <exception cref="InvalidOperationException"></exception>
        void CheckLength(int length)
        {
            if (length > Remaining)
                throw new InvalidOperationException("Cannot Allocate any More Characters");
        }
        #endregion

        public void Clear()
        {
            position = 0;
        }

        public bool Replace(char target, char replacement) => Replace(target, replacement, StringComparison.Ordinal);
        public bool Replace(char target, char replacement, StringComparison comparison)
        {
            var changed = false;

            for (int i = 0; i < position; i++)
            {
                if (characters[i].Equals(target, comparison))
                {
                    changed = true;
                    characters[i] = replacement;
                }
            }

            return changed;
        }

        public bool Replace(ReadOnlySpan<char> target, ReadOnlySpan<char> replacement) => Replace(target, replacement, StringComparison.Ordinal);
        public bool Replace(ReadOnlySpan<char> target, ReadOnlySpan<char> replacement, StringComparison comparison)
        {
            var range = position;

            Clear();

            var source = replacement.Length > target.Length ? stackalloc char[characters.Length].CopyFrom(characters) : characters;

            var changed = false;
            for (int pointer = 0; pointer < range; /*Manually Increment*/)
            {
                var slice = source.Slice(pointer);

                if (MemoryExtensions.StartsWith(slice, target, comparison))
                {
                    Append(replacement);
                    pointer += target.Length;
                    changed = true;
                }
                else
                {
                    Append(source[pointer]);
                    pointer += 1;
                }
            }
            return changed;
        }

        public ReadOnlySpan<char> ToSpan()
        {
            return characters.Slice(0, position);
        }
        public override string ToString()
        {
            var span = ToSpan();
            return new string(span);
        }

        public SpanStringBuilder(Span<char> characters)
        {
            this.characters = characters;
            this.size = characters.Length;

            position = 0;
        }
    }
}