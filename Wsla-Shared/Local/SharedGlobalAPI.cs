using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System;

namespace Wsla
{
    public static class SharedGlobalAPI
    {
        public static async void Forget(this Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                NetworkLog.Error(ex);
            }
        }
        public static async void Forget<T>(this Task<T> task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                NetworkLog.Error(ex);
            }
        }

        public static async void Forget(this ValueTask task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                NetworkLog.Error(ex);
            }
        }
        public static async void Forget<T>(this ValueTask<T> task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                NetworkLog.Error(ex);
            }
        }

        public static string FormatString<T>(this IEnumerable<T> collection)
        {
            return FormatString(collection, x => x.ToString());
        }
        public static string FormatString<T>(this IEnumerable<T> collection, Func<T, string> formatter)
        {
            if (collection is null)
                return "NULL";

            var builder = new StringBuilder();

            builder.Append('[');

            var index = 0;
            foreach (var element in collection)
            {
                if (index is not 0)
                    builder.Append(", ");

                builder.Append(formatter(element));

                index += 1;
            }

            builder.Append(']');

            return builder.ToString();
        }
    }
}