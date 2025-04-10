using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;

using System;
using System.Collections.Generic;

namespace Wsla.Server
{
    public static class CoordinatorServerExtensions
    {
        public static ProviderException ToProviderException(this RestResponse response)
        {
            var code = (ResponseStatus)response.Code;
            return new ProviderException((ResponseStatus)response.Code, response.Message);
        }

        public static bool Contains<T>(this Span<T> span, T entry) => Contains((ReadOnlySpan<T>)span, entry);
        public static bool Contains<T>(this ReadOnlySpan<T> span, T entry)
        {
            var equality = EqualityComparer<T>.Default;

            for (int i = 0; i < span.Length; i++)
                if (equality.Equals(span[i], entry))
                    return true;

            return false;
        }
    }
}