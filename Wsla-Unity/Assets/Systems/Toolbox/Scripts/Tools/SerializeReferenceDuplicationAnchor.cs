#if UNITY_EDITOR
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

using CObject = System.Object;
using UObject = UnityEngine.Object;

namespace Toolbox
{
    /// <summary>
    /// A tool that will properly duplicate all serialized references inside a unity object
    /// </summary>
    public static class SerializeReferenceDuplicationAnchor
    {
        static HashSet<CObject> References = new(200, ReferenceEqualityComparer.Default);
        class ReferenceEqualityComparer : IEqualityComparer<CObject>
        {
            //Ensure we always do a reference check
            public new bool Equals(CObject x, CObject y) => ReferenceEquals(x, y);

            //Default hashcode implementation of the type is good enough
            //I wanted to use the CLRs' internal hashcode mechanism, but I couldn't find a public API for it
            public int GetHashCode(CObject obj) => obj.GetHashCode();

            public static ReferenceEqualityComparer Default { get; } = new ReferenceEqualityComparer();
        }

        public static void Validate(UObject target)
        {
            References.Clear();

            var managedObject = new SerializedObject(target);

            var iterator = managedObject.GetIterator();

            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType is not SerializedPropertyType.ManagedReference)
                    continue;

                if (iterator.managedReferenceValue == null)
                    continue;

                if (References.Add(iterator.managedReferenceValue))
                    continue;

                iterator.managedReferenceValue = DuplicateReference(iterator.managedReferenceValue);
            }

            managedObject.ApplyModifiedProperties();
        }

        static CObject DuplicateReference(CObject original)
        {
            //Yeah, not the most optimal solution, but not many options that Unity allows us

            var type = original.GetType();

            //Json serialization uses the same serialization engine that the inspector uses
            //Ie, we will get all the values we are expecting
            var json = JsonUtility.ToJson(original);

            var clone = JsonUtility.FromJson(json, type);

            return clone;
        }
    }
}
#endif