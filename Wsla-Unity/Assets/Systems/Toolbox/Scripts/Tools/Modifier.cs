using System;
using System.Collections.Generic;

using System.Runtime.CompilerServices;

namespace Toolbox
{
    public static class Modifier
    {
        public class Constraint : Base<bool>
        {
            public override bool Value
            {
                get
                {
                    for (int i = 0; i < List.Count; i++)
                        if (List[i].Invoke())
                            return true;

                    return false;
                }
            }
        }

        public class Average : Base<float>
        {
            public override float Value
            {
                get
                {
                    var result = 0f;

                    for (int i = 0; i < List.Count; i++)
                        result += List[i].Invoke();

                    if (List.Count == 0) return result;

                    return result / List.Count;
                }
            }
        }

        public class Additive : Base<float>
        {
            public float Initial { get; private set; }

            public override float Value
            {
                get
                {
                    var result = Initial;

                    for (int i = 0; i < List.Count; i++)
                        result += List[i].Invoke();

                    return result;
                }
            }

            public Additive(float initial)
            {
                this.Initial = initial;
            }
        }

        public class Scale : Base<float>
        {
            public override float Value
            {
                get
                {
                    var value = 1f;

                    for (int i = 0; i < List.Count; i++)
                        value *= List[i].Invoke();

                    return value;
                }
            }
        }

        public unsafe class Enum<T> : Base<T>
            where T : unmanaged, IComparable, IConvertible, IFormattable
        {
            public override T Value
            {
                get
                {
                    switch (sizeof(T))
                    {
                        case 1:
                        {
                            byte value = 0;

                            for (int i = 0; i < List.Count; i++)
                            {
                                var modifier = List[i].Invoke();
                                value |= Unsafe.As<T, byte>(ref modifier);
                            }

                            return Unsafe.As<byte, T>(ref value);
                        }

                        case 2:
                        {
                            short value = 0;

                            for (int i = 0; i < List.Count; i++)
                            {
                                var modifier = List[i].Invoke();
                                value |= Unsafe.As<T, short>(ref modifier);
                            }

                            return Unsafe.As<short, T>(ref value);
                        }

                        case 4:
                        {
                            int value = 0;

                            for (int i = 0; i < List.Count; i++)
                            {
                                var modifier = List[i].Invoke();
                                value |= Unsafe.As<T, int>(ref modifier);
                            }

                            return Unsafe.As<int, T>(ref value);
                        }

                        case 8:
                        {
                            long value = 0;

                            for (int i = 0; i < List.Count; i++)
                            {
                                var modifier = List[i].Invoke();
                                value |= Unsafe.As<T, long>(ref modifier);
                            }

                            return Unsafe.As<long, T>(ref value);
                        }

                        default:
                            throw new NotImplementedException();
                    }
                }
            }
        }

        public abstract class Base<T>
        {
            public abstract T Value { get; }

            public List<Delegate> List { get; protected set; }
            public delegate T Delegate();

            public T Retrieve() => Value;

            public void Add(Delegate item) => List.Add(item);
            public void Remove(Delegate item) => List.Remove(item);

            public void Clear() => List.Clear();

            public Base()
            {
                List = new List<Delegate>();
            }
        }
    }
}