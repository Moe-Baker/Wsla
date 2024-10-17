using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Toolbox
{
    public static class TypeUtility
    {
        /// <summary>
        /// Checks if an object can be constructed from this type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool CanBeConstructed(this Type type)
        {
            if (type.IsAbstract)
                return false;

            return true;
        }

        public static string FormatBacingFieldName(string name) => $"<{name}>k__BackingField";

        public static bool HasCustomAttribute<T>(this Type type)
            where T : Attribute
        {
            var attribute = type.GetCustomAttribute<T>();
            return attribute is not null;
        }
        public static bool TryGetCustomAttribute<T>(this Type type, out T attribute)
            where T : Attribute
        {
            attribute = type.GetCustomAttribute<T>();
            return attribute is not null;
        }

        public static bool IsAssignableFrom<TDerived>(this Type parent, in TDerived derived)
        {
            //Pass in a generic to avoid allocation if passing in value type, then use .GetType to get the true type
            return parent.IsAssignableFrom(derived.GetType());
        }

        public static TDelegate CreateDelegate<TDelegate>(this MethodInfo method)
            where TDelegate : Delegate
        {
            var type = typeof(TDelegate);
            return method.CreateDelegate(type) as TDelegate;
        }
        public static TDelegate CreateDelegate<TDelegate>(this MethodInfo method, object target)
            where TDelegate : Delegate
        {
            var type = typeof(TDelegate);
            return method.CreateDelegate(type, target) as TDelegate;
        }

        public static unsafe bool HasFlagFast<T>(this T target, T flag)
            where T : unmanaged, Enum
        {
            var size = sizeof(T);

            switch (size)
            {
                case 1:
                    return (Unsafe.As<T, byte>(ref target) & Unsafe.As<T, byte>(ref flag)) > 0;

                case 2:
                    return (Unsafe.As<T, ushort>(ref target) & Unsafe.As<T, ushort>(ref flag)) > 0;

                case 4:
                    return (Unsafe.As<T, uint>(ref target) & Unsafe.As<T, uint>(ref flag)) > 0;

                case 8:
                    return (Unsafe.As<T, ulong>(ref target) & Unsafe.As<T, ulong>(ref flag)) > 0;

                default:
                    throw new ArgumentException($"Invalid Enum with Size of {size}");
            }
        }

        public static bool IsDefined<TEnum>(this TEnum value)
            where TEnum : Enum
        {
            return Enum.IsDefined(typeof(TEnum), value);
        }
    }
}