using System;
using System.Collections.Generic;

namespace Wsla
{
    public interface IReadonlySpannable<T>
    {
        ReadOnlySpan<T> AsSpan();
    }

    public interface ISpannable<T> : IReadonlySpannable<T>
    {
        Span<T> GetUsedSpan();

        ReadOnlySpan<T> IReadonlySpannable<T>.AsSpan() => GetUsedSpan();
    }

    public interface IAssignableSpannable<T>
    {
        void Assign(ReadOnlySpan<T> input);
    }

    public static class SpannableExtensions
    {
        #region Contains
        public static bool Contains<TSpannable, T>(this TSpannable spannable, T item)
            where TSpannable : IReadonlySpannable<T>
        {
            var comparer = EqualityComparer<T>.Default;

            var span = spannable.AsSpan();

            for (int i = 0; i < span.Length; i++)
                if (comparer.Equals(span[i], item))
                    return true;

            return false;
        }

        public static bool Contains<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> characters)
            where TSpannable : IReadonlySpannable<char>
        {
            return spannable.Contains(characters, StringComparison.Ordinal);
        }
        public static bool Contains<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> characters, StringComparison comparison)
            where TSpannable : IReadonlySpannable<char>
        {
            var span = spannable.AsSpan();
            return span.Contains(characters, comparison);
        }
        #endregion

        #region Index Of
        public static int IndexOf<TSpannable, T>(this TSpannable spannable, T item)
            where TSpannable : IReadonlySpannable<T>
        {
            var comparer = EqualityComparer<T>.Default;

            var span = spannable.AsSpan();

            for (int i = 0; i < span.Length; i++)
                if (comparer.Equals(span[i], item))
                    return i;

            return -1;
        }

        public static int IndexOf<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> value)
            where TSpannable : IReadonlySpannable<char>
        {
            var span = spannable.AsSpan();

            return MemoryExtensions.IndexOf(span, value);
        }
        public static int IndexOf<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> value, StringComparison comparison)
            where TSpannable : IReadonlySpannable<char>
        {
            var span = spannable.AsSpan();

            return MemoryExtensions.IndexOf(span, value, comparison);
        }
        #endregion
        #region Last Index Of
        public static int LastIndexOf<TSpannable, T>(this TSpannable spannable, T item)
            where TSpannable : IReadonlySpannable<T>
        {
            var comparer = EqualityComparer<T>.Default;

            var index = -1;

            var span = spannable.AsSpan();

            for (int i = 0; i < span.Length; i++)
                if (comparer.Equals(span[i], item))
                    index = i;

            return index;
        }

        public static int LastIndexOf<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> value)
            where TSpannable : IReadonlySpannable<char>
        {
            var span = spannable.AsSpan();

            return MemoryExtensions.LastIndexOf(span, value);
        }
        #endregion

        #region Replace
        public static void Replace<TSpannable, T>(this ref TSpannable spannable, T item, T replacement)
            where TSpannable : struct, ISpannable<T>
        {
            var comparer = EqualityComparer<T>.Default;

            var span = spannable.GetUsedSpan();

            for (int i = 0; i < span.Length; i++)
            {
                ref var element = ref span[i];

                if (comparer.Equals(element, item))
                    element = replacement;
            }
        }
        public static void Replace<TSpannable, T>(this TSpannable spannable, T item, T replacement)
            where TSpannable : class, ISpannable<T>
        {
            var comparer = EqualityComparer<T>.Default;

            var span = spannable.GetUsedSpan();

            for (int i = 0; i < span.Length; i++)
            {
                ref var element = ref span[i];

                if (comparer.Equals(element, item))
                    element = replacement;
            }
        }
        #endregion

        #region Trim
        public static void Trim<TSpannable>(this ref TSpannable spannable)
            where TSpannable : struct, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.Trim(span);
            spannable.Assign(span);
        }
        public static void Trim<TSpannable>(this TSpannable spannable)
            where TSpannable : class, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.Trim(span);
            spannable.Assign(span);
        }

        public static void Trim<TSpannable>(this ref TSpannable spannable, char character)
            where TSpannable : struct, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.Trim(span, character);
            spannable.Assign(span);
        }
        public static void Trim<TSpannable>(this TSpannable spannable, char character)
            where TSpannable : class, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.Trim(span, character);
            spannable.Assign(span);
        }

        public static void Trim<TSpannable>(this ref TSpannable spannable, ReadOnlySpan<char> characters)
            where TSpannable : struct, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.Trim(span, characters);
            spannable.Assign(span);
        }
        public static void Trim<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> characters)
            where TSpannable : class, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.Trim(span, characters);
            spannable.Assign(span);
        }
        #endregion
        #region TrimStart
        public static void TrimStart<TSpannable>(this ref TSpannable spannable)
            where TSpannable : struct, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimStart(span);
            spannable.Assign(span);
        }
        public static void TrimStart<TSpannable>(this TSpannable spannable)
            where TSpannable : class, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimStart(span);
            spannable.Assign(span);
        }

        public static void TrimStart<TSpannable>(this ref TSpannable spannable, char character)
            where TSpannable : struct, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimStart(span, character);
            spannable.Assign(span);
        }
        public static void TrimStart<TSpannable>(this TSpannable spannable, char character)
            where TSpannable : class, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimStart(span, character);
            spannable.Assign(span);
        }

        public static void TrimStart<TSpannable>(this ref TSpannable spannable, ReadOnlySpan<char> characters)
            where TSpannable : struct, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimStart(span, characters);
            spannable.Assign(span);
        }
        public static void TrimStart<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> characters)
            where TSpannable : class, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimStart(span, characters);
            spannable.Assign(span);
        }
        #endregion
        #region TrimEnd
        public static void TrimEnd<TSpannable>(this ref TSpannable spannable)
            where TSpannable : struct, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimEnd(span);
            spannable.Assign(span);
        }
        public static void TrimEnd<TSpannable>(this TSpannable spannable)
            where TSpannable : class, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimEnd(span);
            spannable.Assign(span);
        }

        public static void TrimEnd<TSpannable>(this ref TSpannable spannable, char character)
            where TSpannable : struct, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimEnd(span, character);
            spannable.Assign(span);
        }
        public static void TrimEnd<TSpannable>(this TSpannable spannable, char character)
            where TSpannable : class, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimEnd(span, character);
            spannable.Assign(span);
        }

        public static void TrimEnd<TSpannable>(this ref TSpannable spannable, ReadOnlySpan<char> characters)
            where TSpannable : struct, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimEnd(span, characters);
            spannable.Assign(span);
        }
        public static void TrimEnd<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> characters)
            where TSpannable : class, ISpannable<char>, IAssignableSpannable<char>
        {
            var span = spannable.GetUsedSpan();

            var input = MemoryExtensions.TrimEnd(span, characters);
            spannable.Assign(span);
        }
        #endregion

        #region Starts With
        public static bool StartsWith<TSpannable, T>(this TSpannable spannable, ReadOnlySpan<T> values)
            where TSpannable : IReadonlySpannable<T>
            where T : IEquatable<T>
        {
            var span = spannable.AsSpan();
            return span.StartsWith(values);
        }

        public static bool StartsWith<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> values)
            where TSpannable : IReadonlySpannable<char>
        {
            var span = spannable.AsSpan();
            return span.StartsWith(values, StringComparison.Ordinal);
        }
        public static bool StartsWith<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> values, StringComparison comparison)
            where TSpannable : IReadonlySpannable<char>
        {
            var span = spannable.AsSpan();
            return span.StartsWith(values, comparison);
        }
        #endregion
        #region Ends With
        public static bool EndsWith<TSpannable, T>(this TSpannable spannable, ReadOnlySpan<T> values)
            where TSpannable : IReadonlySpannable<T>
            where T : IEquatable<T>
        {
            var span = spannable.AsSpan();
            return span.EndsWith(values);
        }

        public static bool EndsWith<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> values)
            where TSpannable : IReadonlySpannable<char>
        {
            var span = spannable.AsSpan();
            return span.EndsWith(values, StringComparison.Ordinal);
        }
        public static bool EndsWith<TSpannable>(this TSpannable spannable, ReadOnlySpan<char> values, StringComparison comparison)
            where TSpannable : IReadonlySpannable<char>
        {
            var span = spannable.AsSpan();
            return span.EndsWith(values, comparison);
        }
        #endregion

        #region Reverse
        public static void Reverse<TSpannable, T>(this ref TSpannable spannable)
            where TSpannable : struct, ISpannable<T>
        {
            var span = spannable.GetUsedSpan();
            span.Reverse();
        }
        public static void Reverse<TSpannable, T>(this TSpannable spannable)
            where TSpannable : class, ISpannable<T>
        {
            var span = spannable.GetUsedSpan();
            span.Reverse();
        }

        public static void Reverse<TSpannable>(this ref TSpannable spannable)
            where TSpannable : struct, ISpannable<char>
        {
            var span = spannable.GetUsedSpan();
            span.Reverse();
        }
        public static void Reverse<TSpannable>(this TSpannable spannable)
            where TSpannable : class, ISpannable<char>
        {
            var span = spannable.GetUsedSpan();
            span.Reverse();
        }
        #endregion

        #region To Lower
        public static void ToLower<TSpannable>(this ref TSpannable spannable)
            where TSpannable : struct, ISpannable<char>
        {
            var span = spannable.GetUsedSpan();

            for (int i = 0; i < span.Length; i++)
            {
                ref var element = ref span[i];
                element = char.ToLower(element);
            }
        }
        public static void ToLower<TSpannable>(this TSpannable spannable)
            where TSpannable : class, ISpannable<char>
        {
            var span = spannable.GetUsedSpan();

            for (int i = 0; i < span.Length; i++)
            {
                ref var element = ref span[i];
                element = char.ToLower(element);
            }
        }
        #endregion
        #region To Upper
        public static void ToUpper<TSpannable>(this ref TSpannable spannable)
            where TSpannable : struct, ISpannable<char>
        {
            var span = spannable.GetUsedSpan();

            for (int i = 0; i < span.Length; i++)
            {
                ref var element = ref span[i];
                element = char.ToUpper(element);
            }
        }
        public static void ToUpper<TSpannable>(this TSpannable spannable)
            where TSpannable : class, ISpannable<char>
        {
            var span = spannable.GetUsedSpan();

            for (int i = 0; i < span.Length; i++)
            {
                ref var element = ref span[i];
                element = char.ToUpper(element);
            }
        }
        #endregion

        public static void CopyTo<TSpannable, T>(this TSpannable spannable, Span<T> destination)
            where TSpannable : IReadonlySpannable<T>
        {
            var source = spannable.AsSpan();
            source.CopyTo(destination);
        }
    }
}