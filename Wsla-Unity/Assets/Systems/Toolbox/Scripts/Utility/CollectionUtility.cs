using System;
using System.Collections.Generic;

namespace Toolbox
{
    public static class CollectionUtility
    {
        public static void ForAll<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
                action(item);
        }

        public static T GetRandom<T>(this IList<T> list)
        {
            var index = UnityEngine.Random.Range(0, list.Count);
            return list[index];
        }

        /// <summary>
        /// Shuffles the order of the element in this collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        public static void Shuffle<T>(this IList<T> list)
        {
            for (int source = 0; source < list.Count; source++)
            {
                var destination = UnityEngine.Random.Range(0, list.Count);

                (list[source], list[destination]) = (list[destination], list[source]);
            }
        }

        public static ArraySegment<T> Segment<T>(this T[] array, int count) => new(array, 0, count);
        public static ArraySegment<T> Segment<T>(this T[] array, int offset, int count) => new(array, offset, count);

        public static bool IsValidIndex<T>(this IList<T> list, int index) => index < list.Count && index > 0;
    }
}