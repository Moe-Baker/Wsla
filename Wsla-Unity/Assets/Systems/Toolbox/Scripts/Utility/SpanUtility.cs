using System;

namespace Toolbox
{
    public static class SpanUtility
    {
        public static Span<T> CopyFrom<T>(this Span<T> destination, Span<T> source)
        {
            source.CopyTo(destination);
            return destination;
        }
    }
}