using System;

namespace Wsla
{
    /// <summary>
    /// Generic utility for Type.(TryParse/TryFormat) semantics, to allow generic access to it
    /// </summary>
    public class TextValueConverter
    {
        static class Collection<T>
        {
            public static ParseDelegate<T> Parser;
            public static FormatDelegate<T> Formatter;
        }
        public delegate bool ParseDelegate<T>(ReadOnlySpan<char> characters, out T value);
        public delegate bool FormatDelegate<T>(T value, Span<char> destination, out int written);

        public static bool TryParse<T>(ReadOnlySpan<char> text, out T value)
        {
            var parser = Collection<T>.Parser;

            if (parser is null)
                throw new InvalidOperationException($"No Text Value Parser Registered for {(typeof(T))}");

            return parser(text, out value);
        }
        public static bool TryFormat<T>(T value, Span<char> destination, out int written)
        {
            var formatter = Collection<T>.Formatter;

            if (formatter is null)
                throw new InvalidOperationException($"No Text Value Parser Registered for {(typeof(T))}");

            return formatter(value, destination, out written);
        }

        public static void Register<T>(ParseDelegate<T> parser, FormatDelegate<T> formatter)
        {
            Collection<T>.Parser = parser;
            Collection<T>.Formatter = formatter;
        }

        static TextValueConverter()
        {
            Register(byte.TryParse, (byte value, Span<char> destination, out int written) => value.TryFormat(destination, out written));
            Register(sbyte.TryParse, (sbyte value, Span<char> destination, out int written) => value.TryFormat(destination, out written));

            Register(short.TryParse, (short value, Span<char> destination, out int written) => value.TryFormat(destination, out written));
            Register(ushort.TryParse, (ushort value, Span<char> destination, out int written) => value.TryFormat(destination, out written));

            Register(int.TryParse, (int value, Span<char> destination, out int written) => value.TryFormat(destination, out written));
            Register(uint.TryParse, (uint value, Span<char> destination, out int written) => value.TryFormat(destination, out written));

            Register(long.TryParse, (long value, Span<char> destination, out int written) => value.TryFormat(destination, out written));
            Register(ulong.TryParse, (ulong value, Span<char> destination, out int written) => value.TryFormat(destination, out written));

            Register(float.TryParse, (float value, Span<char> destination, out int written) => value.TryFormat(destination, out written));
            Register(double.TryParse, (double value, Span<char> destination, out int written) => value.TryFormat(destination, out written));
            Register(decimal.TryParse, (decimal value, Span<char> destination, out int written) => value.TryFormat(destination, out written));

            Register(Guid.TryParse, (Guid value, Span<char> destination, out int written) => value.TryFormat(destination, out written));

            Register(DateTime.TryParse, (DateTime value, Span<char> destination, out int written) => value.TryFormat(destination, out written));

            Register(TimeSpan.TryParse, (TimeSpan value, Span<char> destination, out int written) => value.TryFormat(destination, out written));
        }
    }
}