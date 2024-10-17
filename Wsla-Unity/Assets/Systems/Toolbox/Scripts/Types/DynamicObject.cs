using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Toolbox
{
    /// <summary>
    /// A class that will conrain an object and enable access to it's members via IL code generation
    /// </summary>
    public struct DynamicObject
    {
        public object Context { get; }
        public Type Type => Context?.GetType();
        public bool IsNull => Context == null;
        public bool IsStruct => Type.IsValueType;

        const BindingFlags Bindings = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public DynamicObject this[int index] => GetElement(index);

        public DynamicObject GetElement(int index)
        {
            if (Context is not IList list)
                throw new InvalidOperationException($"Cannot Index Type {Type}");

            var value = list[index];

            return new DynamicObject(value);
        }
        public void SetElement(int index, object value)
        {
            if (Context is not IList list)
                throw new InvalidOperationException($"Cannot Index Type {Type}");

            list[index] = value;
        }

        public DynamicObject GetField(string name)
        {
            var getter = DynamicObjectSigilsCache.Getters.Retrieve(Type, name);
            var value = getter.InvokeImplicit(Context);
            return new DynamicObject(value);
        }
        public object SetField(string name, object value)
        {
            var source = Context;

            var setter = DynamicObjectSigilsCache.Setters.Retrieve(Type, name);
            setter.InvokeImplicit(ref source, value);

            return source;
        }

        public object InvokeMethod(string name, params object[] parameters)
        {
            var info = Type.GetMethod(name, Bindings);
            if (info is null)
                throw new ArgumentException($"No Method Named ({name}) Found on {this}");

            return info.Invoke(Context, parameters);
        }

        public override string ToString() => Context?.ToString();

        public DynamicObject(object self)
        {
            this.Context = self;
        }
    }

    /// <summary>
    /// A class that will contain an entire dynamic object's traversal path,
    /// helps with setting values on structs,
    /// supports up-to 10 levels of nesting
    /// </summary>
    public struct DynamicObjectHierarchy
    {
        Entry item0, item1, item2, item3, item4, item5, item6, item7, item8, item9;
        public struct Entry
        {
            public string Name { get; }
            public int Index { get; }

            public TypeMode Type { get; }
            public enum TypeMode
            {
                Field,
                Element
            }

            public DynamicObject Object { get; }
            public object Context => Object.Context;

            public Entry(string name, DynamicObject target)
            {
                this.Name = name;
                this.Object = target;

                Type = TypeMode.Field;
                Index = default;
            }

            public Entry(int index, DynamicObject target)
            {
                this.Index = index;
                this.Object = target;

                Type = TypeMode.Element;
                Name = default;
            }
        }

        public int Count { get; private set; }

        public const int Capacity = 10;

        public Entry this[Index index]
        {
            get
            {
                var target = index.GetOffset(Count);

                if (target >= Count || target < 0)
                    throw new ArgumentOutOfRangeException($"Value must be Between 0-{Capacity - 1}");

                switch (target)
                {
                    case 0: return item0;
                    case 1: return item1;
                    case 2: return item2;
                    case 3: return item3;
                    case 4: return item4;
                    case 5: return item5;
                    case 6: return item6;
                    case 7: return item7;
                    case 8: return item8;
                    case 9: return item9;

                    default: throw new NotImplementedException();
                }
            }
            set
            {
                var target = index.GetOffset(Count);

                if (target >= Count || target < 0)
                    throw new ArgumentOutOfRangeException($"Value must be Between 0-{Capacity - 1}");

                switch (index.GetOffset(Count))
                {
                    case 0: item0 = value; break;
                    case 1: item1 = value; break;
                    case 2: item2 = value; break;
                    case 3: item3 = value; break;
                    case 4: item4 = value; break;
                    case 5: item5 = value; break;
                    case 6: item6 = value; break;
                    case 7: item7 = value; break;
                    case 8: item8 = value; break;
                    case 9: item9 = value; break;

                    default: throw new NotImplementedException();
                }
            }
        }

        public Entry Self => this[^1];

        public DynamicObject GetElement(int index)
        {
            return Self.Object.GetElement(index);
        }
        public void SetElement(int index, object value)
        {
            Self.Object.SetElement(index, value);
        }

        public DynamicObject GetField(string name)
        {
            return Self.Object.GetField(name);
        }
        public void SetField(string name, object value)
        {
            var self = this[^1];

            value = self.Object.SetField(name, value);

            for (int i = Count - 1 - 1; i >= 0; i--)
            {
                if (self.Object.IsStruct is false)
                    break;

                var parent = this[i];

                switch (self.Type)
                {
                    case Entry.TypeMode.Field:
                        value = parent.Object.SetField(self.Name, value);
                        break;

                    case Entry.TypeMode.Element:
                        parent.Object.SetElement(self.Index, value);
                        break;
                }

                self = parent;
            }
        }

        public DynamicObjectHierarchy AddElement(int index)
        {
            var element = Self.Object.GetElement(index);
            var entry = new Entry(index, element);

            Add(entry);

            return this;
        }
        public DynamicObjectHierarchy AddField(string name)
        {
            var field = Self.Object.GetField(name);
            var entry = new Entry(name, field);

            Add(entry);

            return this;
        }

        public void Add(Entry entry)
        {
            if (Count >= Capacity)
                throw new InvalidOperationException($"Capacity Reached");

            var index = Count;

            Count += 1;

            this[index] = entry;
        }

        public DynamicObjectHierarchy Pop()
        {
            Count -= 1;
            return this;
        }

        public DynamicObjectHierarchy Clone() => this; //Yes, it's that easy

        public DynamicObjectHierarchy(in DynamicObject root)
        {
            Unsafe.SkipInit(out this);

            item0 = new Entry("root", root);
            Count = 1;
        }

        public static DynamicObjectHierarchy From(object root)
        {
            var target = new DynamicObject(root);
            return new DynamicObjectHierarchy(in target);
        }
    }

    public static class DynamicObjectSigilsCache
    {
        const BindingFlags Bindings = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static class Getters
        {
            static Dictionary<Key, IContract> Dictionary;

            public interface IContract
            {
                object InvokeImplicit(object source);
            }
            public class Machine<TSource, TValue> : IContract
            {
                Signature<TSource, TValue> Lambda;

                public object InvokeImplicit(object source)
                {
                    return InvokeExplicit((TSource)source);
                }
                public TValue InvokeExplicit(TSource source)
                {
                    return Lambda.Invoke(source);
                }

                public Machine(FieldInfo info)
                {
                    Lambda = Create<TSource, TValue>(info);
                }
            }

            public static IContract Retrieve(Type type, string field)
            {
                var key = new Key(type, field);

                if (Dictionary.TryGetValue(key, out var getter))
                    return getter;

                var info = type.GetField(field, Bindings);

                var template = typeof(Machine<,>).MakeGenericType(type, info.FieldType);
                getter = Activator.CreateInstance(template, args: new object[] { info }) as IContract;

                Dictionary[key] = getter;

                return getter;
            }

            static Signature<TSource, TValue> Create<TSource, TValue>(FieldInfo info)
            {
                var emitter = Sigil.Emit<Signature<TSource, TValue>>.NewDynamicMethod(doVerify: Application.isEditor);

                emitter.LoadArgument(0);
                emitter.LoadField(info);
                emitter.Return();

                return emitter.CreateDelegate();
            }

            public delegate TValue Signature<TSource, TValue>(TSource source);

            static Getters()
            {
                Dictionary = new();
            }
        }
        public static class Setters
        {
            static Dictionary<Key, IContract> Dictionary;

            public interface IContract
            {
                void InvokeImplicit(ref object source, object value);
            }
            public class Machine<TSource, TValue> : IContract
            {
                Signature<TSource, TValue> Lambda;

                public void InvokeImplicit(ref object source, object value)
                {
                    (TSource Source, TValue Value) cast;

                    try
                    {
                        cast.Source = (TSource)source;
                    }
                    catch (InvalidCastException)
                    {
                        throw new ArgumentException($"Can't Convert ({source.GetType()}) to ({typeof(TSource)})");
                    }

                    try
                    {
                        cast.Value = (TValue)value;
                    }
                    catch (InvalidCastException)
                    {
                        throw new ArgumentException($"Can't Convert ({value.GetType()}) to ({typeof(TValue)})");
                    }

                    InvokeExplicit(ref cast.Source, cast.Value);

                    source = cast.Source;
                }
                public void InvokeExplicit(ref TSource source, TValue value)
                {
                    Lambda.Invoke(ref source, value);
                }

                public Machine(FieldInfo info)
                {
                    Lambda = Create<TSource, TValue>(info);
                }
            }

            public static IContract Retrieve(Type type, string field)
            {
                var key = new Key(type, field);

                if (Dictionary.TryGetValue(key, out var setter))
                    return setter;

                var info = type.GetField(field, Bindings);

                var template = typeof(Machine<,>).MakeGenericType(type, info.FieldType);
                setter = Activator.CreateInstance(template, args: new object[] { info }) as IContract;

                Dictionary[key] = setter;

                return setter;
            }

            static Signature<TSource, TValue> Create<TSource, TValue>(FieldInfo info)
            {
                var emitter = Sigil.Emit<Signature<TSource, TValue>>.NewDynamicMethod(doVerify: Application.isEditor);

                emitter.LoadArgument(0);

                if (typeof(TSource).IsValueType is false)
                    emitter.LoadIndirect<TSource>();

                emitter.LoadArgument(1);

                emitter.StoreField(info);

                emitter.Return();

                return emitter.CreateDelegate();
            }

            public delegate void Signature<TSource, TValue>(ref TSource source, TValue value);

            static Setters()
            {
                Dictionary = new();
            }
        }

        public static (Getters.IContract, Setters.IContract) GetAccessors(Type type, string name)
        {
            var setter = Setters.Retrieve(type, name);
            var getter = Getters.Retrieve(type, name);

            return (getter, setter);
        }

        public struct Key : IEquatable<Key>
        {
            public Type Type { get; }
            public string Name { get; }

            public override int GetHashCode() => HashCode.Combine(Type, Name);

            public override bool Equals(object obj)
            {
                if (obj is Key other)
                    return Equals(other);

                return false;
            }
            public bool Equals(Key other)
            {
                if (Type != other.Type)
                    return false;

                if (Name != other.Name)
                    return false;

                return true;
            }

            public Key(Type type, string name)
            {
                this.Type = type;
                this.Name = name;
            }
        }
    }
}