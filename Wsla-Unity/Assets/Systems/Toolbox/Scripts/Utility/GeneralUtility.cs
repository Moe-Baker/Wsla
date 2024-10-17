using System;

namespace Toolbox
{
    public static class GeneralUtility
    {
        public static int Compare<T>(T left, T right) where T : IComparable<T> => left.CompareTo(right);

        public static void Swap<T>(ref T x, ref T y) => (x, y) = (y, x);
    }
}