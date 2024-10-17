using System;
using System.Collections.Generic;

using UnityEngine;

namespace Toolbox
{
    public static class BoundsUtility
    {
        static List<Renderer> RendererCache;
        static List<Collider> ColliderCache;

        static BoundsUtility()
        {
            RendererCache = new();
            ColliderCache = new();
        }

        public static Bounds CalculateRenderer(GameObject target) => CalculateRenderer(target, true);
        public static Bounds CalculateRenderer(GameObject target, bool includeInactive)
        {
            var list = RendererCache;

            target.GetComponentsInChildren(includeInactive, list);

            Span<Bounds> span = stackalloc Bounds[list.Count];

            for (int i = 0; i < list.Count; i++)
                span[i] = list[i].bounds;

            return Combine(target.transform, span);
        }

        public static Bounds CalculateCollider(GameObject target) => CalculateCollider(target, true);
        public static Bounds CalculateCollider(GameObject target, bool includeInactive)
        {
            var list = ColliderCache;

            target.GetComponentsInChildren(includeInactive, list);

            Span<Bounds> span = stackalloc Bounds[list.Count];

            for (int i = 0; i < list.Count; i++)
                span[i] = list[i].bounds;

            return Combine(target.transform, span);
        }

        public static Bounds Combine(Transform root, Span<Bounds> span)
        {
            if (span.Length == 0)
                return new Bounds(root.position, Vector3.zero);

            var bound = span[0];

            for (int i = 1; i < span.Length; i++)
                bound.Encapsulate(span[i]);

            bound.center = root.InverseTransformPoint(bound.center);

            return bound;
        }

        public static float Distance(this Bounds bounds, Vector3 point) => MathF.Sqrt(bounds.SqrDistance(point));
    }
}