using System;

using UnityEngine;
using UnityEngine.LowLevel;

namespace Toolbox
{
    public static class PlayerLoopUtility
    {
        public static void Register<TType>(PlayerLoopSystem.UpdateFunction callback, bool autoRemove = true)
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();

            var index = Locate<TType>(ref loop);

            if (index == -1)
                throw new Exception($"No PlayerLoop Entry Found for {typeof(TType)}");

            loop.subSystemList[index].updateDelegate += callback;

            PlayerLoop.SetPlayerLoop(loop);

            if (autoRemove) Application.quitting += () => Unregister<TType>(callback);
        }

        public static int Locate<TType>(ref PlayerLoopSystem loop)
        {
            for (int i = 0; i < loop.subSystemList.Length; ++i)
                if (loop.subSystemList[i].type == typeof(TType))
                    return i;

            return -1;
        }

        public static bool Unregister<TType>(PlayerLoopSystem.UpdateFunction callback)
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();

            var index = Locate<TType>(ref loop);

            if (index == -1) return false;

            loop.subSystemList[index].updateDelegate -= callback;

            PlayerLoop.SetPlayerLoop(loop);
            return true;
        }
    }
}