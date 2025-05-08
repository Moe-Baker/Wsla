using System;
using System.Reflection;

namespace Toolbox
{
    public static class ReflectionUtility
    {
        public static T GetValue<T>(this object target, string variable, VariableQueryTarget query = VariableQueryTarget.Any)
        {
            var type = target.GetType();

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            while (type is not null)
            {
                //Field
                if (query.HasFlag(VariableQueryTarget.Field))
                {
                    var field = type.GetField(variable, flags);

                    if (field is not null)
                        return (T)field.GetValue(target);
                }

                //Property
                if (query.HasFlag(VariableQueryTarget.Property))
                {
                    var field = type.GetField(variable, flags);

                    if (field is not null)
                        return (T)field.GetValue(target);
                }

                type = type.BaseType;
            }

            return default;
        }
    }

    [Flags]
    public enum VariableQueryTarget
    {
        Field = 0 << 1,
        Property = 1 << 1,

        Any = Field | Property,
    }
}