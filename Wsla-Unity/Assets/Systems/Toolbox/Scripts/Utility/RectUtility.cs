using UnityEditor;

using UnityEngine;

namespace Toolbox
{
    public static class RectUtility
    {
        public static float IndentWidth => 15;

        public static Rect SliceVertical(this Rect original, float height) => SliceVertical(original, height, out _);
        public static Rect SliceVertical(this Rect original, float height, out Rect slice)
        {
            slice = new Rect(original)
            {
                height = height,
            };

            return new Rect(original)
            {
                yMin = original.yMin + height,
            };
        }

        public static Rect SliceHorizontal(this Rect original, float width) => SliceHorizontal(original, width, out _);
        public static Rect SliceHorizontal(this Rect original, float width, out Rect slice)
        {
            slice = new Rect(original)
            {
                width = width,
            };

            return new Rect(original)
            {
                xMin = original.xMin + width,
            };
        }

        public static Rect SliceIndent(this Rect original) => SliceHorizontal(original, IndentWidth);

#if UNITY_EDITOR
        public static Rect SliceLine(this Rect original, out Rect slice) => SliceLines(original, 1, out slice);
        public static Rect SliceLines(this Rect original, uint count, out Rect slice)
        {
            var height = EditorGUIUtility.singleLineHeight * count;

            return SliceVertical(original, height, out slice);
        }

        public static Rect SliceStandardSpace(this Rect original) => SliceVertical(original, EditorGUIUtility.standardVerticalSpacing);

        public static Rect SliceFoldoutIndent(this Rect original)
        {
            if (EditorGUIUtility.hierarchyMode == false)
            {
                int offset = (EditorStyles.foldout.padding.left - EditorStyles.label.padding.left);
                original.xMin += offset;
            }

            return SliceIndent(original);
        }

        public static Rect ZeroIndent(this Rect original)
        {
            var area = EditorGUI.IndentedRect(original);
            EditorGUI.indentLevel = 0;
            return area;
        }
#endif

        public static Rect SetCenterHeight(this Rect rect, float height)
        {
            var area = new Rect(rect);

            var delta = area.height - height;
            area.height = height;
            area.center += Vector2.up * (delta / 2);

            return area;
        }

        public static Rect SetCenterWidth(this Rect rect, float width)
        {
            var area = new Rect(rect);

            var delta = area.width - width;
            area.width = width;
            area.center += Vector2.right * (delta / 2);

            return area;
        }
    }
}