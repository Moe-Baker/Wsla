#if UNITY_EDITOR
using UnityEditor;

using UnityEngine;

namespace Toolbox
{
    public abstract partial class HandlesUtility
    {
        public static void DrawWireCapsule(Vector3 position, Quaternion rotation, float radius, float height)
        {
            var matrix = Matrix4x4.TRS(position, rotation, Handles.matrix.lossyScale);

            using (new Handles.DrawingScope(matrix))
            {
                var offset = (height - (radius * 2)) / 2;

                //draw sideways

                Handles.DrawWireArc(Vector3.up * offset, Vector3.left, Vector3.back, -180, radius);
                Handles.DrawLine(new Vector3(0, offset, -radius), new Vector3(0, -offset, -radius));
                Handles.DrawLine(new Vector3(0, offset, radius), new Vector3(0, -offset, radius));
                Handles.DrawWireArc(Vector3.down * offset, Vector3.left, Vector3.back, 180, radius);

                //draw frontways
                Handles.DrawWireArc(Vector3.up * offset, Vector3.back, Vector3.left, 180, radius);
                Handles.DrawLine(new Vector3(-radius, offset, 0), new Vector3(-radius, -offset, 0));
                Handles.DrawLine(new Vector3(radius, offset, 0), new Vector3(radius, -offset, 0));
                Handles.DrawWireArc(Vector3.down * offset, Vector3.back, Vector3.left, -180, radius);

                //draw center
                Handles.DrawWireDisc(Vector3.up * offset, Vector3.up, radius);
                Handles.DrawWireDisc(Vector3.down * offset, Vector3.up, radius);
            }
        }

        public static void DrawWireCapsuleNoCap(Vector3 position, Quaternion rotation, float radius, float height)
        {
            Handles.color = Gizmos.color;

            var matrix = Matrix4x4.TRS(position, rotation, Handles.matrix.lossyScale);

            using (new Handles.DrawingScope(matrix))
            {
                var offset = (height - (radius * 2)) / 2;

                //draw sideways

                Handles.DrawLine(new Vector3(0, offset, -radius), new Vector3(0, -offset, -radius));
                Handles.DrawLine(new Vector3(0, offset, radius), new Vector3(0, -offset, radius));

                //draw frontways
                Handles.DrawLine(new Vector3(-radius, offset, 0), new Vector3(-radius, -offset, 0));
                Handles.DrawLine(new Vector3(radius, offset, 0), new Vector3(radius, -offset, 0));

                //draw center
                Handles.DrawWireDisc(Vector3.zero, Vector3.up, radius);
            }
        }

        public static void DrawWireCylinder(Vector3 position, Quaternion rotation, float radius, float height)
        {
            var matrix = Matrix4x4.TRS(position, rotation, Handles.matrix.lossyScale);

            using (new Handles.DrawingScope(matrix))
            {
                var offset = height / 2;

                //draw sideways
                Handles.DrawLine(new Vector3(0, offset, -radius), new Vector3(0, -offset, -radius));
                Handles.DrawLine(new Vector3(0, offset, radius), new Vector3(0, -offset, radius));

                //draw frontways
                Handles.DrawLine(new Vector3(-radius, offset, 0), new Vector3(-radius, -offset, 0));
                Handles.DrawLine(new Vector3(radius, offset, 0), new Vector3(radius, -offset, 0));

                //draw center
                Handles.DrawWireDisc(Vector3.up * offset, Vector3.up, radius);
                Handles.DrawWireDisc(Vector3.down * offset, Vector3.up, radius);
            }
        }

        public static void DrawArrow(Vector3 position, Vector3 direction, float length = 0.25f, float angle = 20.0f)
        {
            Gizmos.DrawRay(position, direction);

            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + angle, 0) * new Vector3(0, 0, 1);
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - angle, 0) * new Vector3(0, 0, 1);
            Gizmos.DrawRay(position + direction, right * length);
            Gizmos.DrawRay(position + direction, left * length);
        }

        public static void DrawWireCone(Vector3 position, Quaternion rotation, float radius, float height)
        {
            var up = rotation * Vector3.up;
            position -= up * (height / 2f);

            Handles.DrawWireDisc(position, up, radius);

            var end = position + (up * height);

            const int SegmentCount = 4;

            for (int i = 0; i < SegmentCount; i++)
            {
                var angle = 360f * i / SegmentCount;

                var direction = rotation * Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                var start = position + direction * radius;

                Handles.DrawLine(start, end);
            }
        }

        public static void Draw3DArrow(Vector3 position, Quaternion rotation, float radius, float height)
        {
            var up = rotation * Vector3.up;

            //Cone
            {
                var heightD = height * 0.3f;
                var radiusD = radius * 1.75f;

                DrawWireCone(position + up * ((height - heightD) / 2), rotation, radiusD, heightD);

                height -= heightD;
                position -= up * (heightD / 2);
            }

            //Cylinder
            {
                DrawWireCylinder(position, rotation, radius, height);
            }
        }
    }
}
#endif