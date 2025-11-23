using System;
using System.Net;
using System.Numerics;

namespace Wsla
{
    public ref struct SpanStringBuilder
    {
        Span<char> characters;
        public int Capacity => characters.Length;

        public int Position { get; private set; }
        public int Remaining => Capacity - Position;

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

            Position += written;
        }

        public void Append(char character)
        {
            CheckLength(1);
            characters[Position] = character;
            Position += 1;
        }

        public void Append(byte target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }
        public void Append(sbyte target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(short target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }
        public void Append(ushort target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(int target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }
        public void Append(uint target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(long target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }
        public void Append(ulong target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(BigInteger target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(float target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }
        public void Append(double target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }
        public void Append(decimal target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(Guid target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(DateTime target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(DateTimeOffset target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(TimeSpan target, ReadOnlySpan<char> format = default, IFormatProvider provider = default)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written, format, provider) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }

        public void Append(IPAddress target)
        {
            var span = GetRemaining();

            if (target.TryFormat(span, out var written) == false)
                throw new InvalidOperationException($"Cannot Write Formattable Target ({target}), Not Enough Space");

            Position += written;
        }
        #endregion

        public void Increment(int offset)
        {
            if (offset > Remaining)
                throw new ArgumentOutOfRangeException("Cannot Increment Position");

            Position += offset;
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

            var span = characters.Slice(Position, length);

            Position += length;

            return span;
        }

        /// <summary>
        /// Gets the remaining build space
        /// </summary>
        /// <returns></returns>
        public Span<char> GetRemaining()
        {
            var span = characters.Slice(Position, Remaining);

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
            Position = 0;
        }

        public ReadOnlySpan<char> ToSpan()
        {
            return characters.Slice(0, Position);
        }
        public override string ToString()
        {
            var span = ToSpan();
            return new string(span);
        }

        public SpanStringBuilder(Span<char> characters)
        {
            this.characters = characters;
            Position = 0;
        }
    }
}