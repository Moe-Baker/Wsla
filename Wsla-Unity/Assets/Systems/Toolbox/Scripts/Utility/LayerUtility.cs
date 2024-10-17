using System;

using UnityEngine;

namespace Toolbox
{
    public static class LayersUtility
    {
        public static void Set(GameObject context, string name)
        {
            var index = LayerMask.NameToLayer(name);

            if (index < 0)
                throw new ArgumentException($"No Layer with Name ({name}) Found");

            Set(context, index);
        }
        public static void Set(GameObject context, LayerValue value) => Set(context, value.Index);
        public static void Set(GameObject context, int index)
        {
            context.layer = index;

            //Iterate Children
            {
                var count = context.transform.childCount;

                for (int i = 0; i < count; i++)
                    Set(context.transform.GetChild(i).gameObject, index);
            }
        }

        public static bool Contains(this LayerMask mask, LayerValue value) => Contains(mask, value.Index);
        public static bool Contains(this LayerMask mask, int layer) => mask == (mask | (1 << layer));
    }
}