using System;
using System.Collections.Generic;

using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Wsla.Unity
{
    [Serializable]
    public class NetworkUpdateAPI : NetworkAPI.Property
    {
        public override void Set(NetworkAPI value)
        {
            base.Set(value);

            if (NetworkAPI.Runtime.ExecutionContext is not NetworkAPI.ExecutionModeSelection.Runtime)
                return;

            API.OnDispose += Dispose;

            //Early Network Update
            {
                var loop = FindPlayerLoop<EarlyUpdate>();
                loop.Add<EarlyNetworkUpdate>(EarlyUpdate);
            }

            //Late Network Update
            {
                var loop = FindPlayerLoop<PreLateUpdate>();
                loop.Add<LateNetworkUpdate>(LateUpdate);
            }
        }

        void Dispose()
        {
            //Early Network Update
            {
                var loop = FindPlayerLoop<EarlyUpdate>();
                loop.Remove<EarlyNetworkUpdate>();

                OnEarlyUpdate = default;
            }

            //Late Network Update
            {
                var loop = FindPlayerLoop<PreLateUpdate>();
                loop.Remove<LateNetworkUpdate>();

                OnLateUpdate = default;
            }
        }

        public event Action OnEarlyUpdate;
        void EarlyUpdate()
        {
            OnEarlyUpdate?.Invoke();
        }

        public event Action OnLateUpdate;
        void LateUpdate()
        {
            OnLateUpdate?.Invoke();
        }

        static PlayerLoopContext FindPlayerLoop<T>()
        {
            var system = PlayerLoop.GetCurrentPlayerLoop();

            for (int i = 0; i < system.subSystemList.Length; i++)
            {
                if (system.subSystemList[i].type == typeof(T))
                {
                    return new PlayerLoopContext(system, i);
                }
            }

            throw new ArgumentException($"No Player Update Loop Entry Foun for {typeof(T)}");
        }

        struct PlayerLoopContext
        {
            public PlayerLoopSystem Root { get; }

            public int Index { get; }

            public ref PlayerLoopSystem Target => ref Root.subSystemList[Index];

            public void Add<T>(PlayerLoopSystem.UpdateFunction callback)
            {
                var system = new PlayerLoopSystem()
                {
                    type = typeof(T),
                    subSystemList = null,
                    updateDelegate = callback,

                    loopConditionFunction = Target.loopConditionFunction,
                    updateFunction = Target.updateFunction,
                };

                Add(system);
            }
            public void Add(PlayerLoopSystem system)
            {
                Array.Resize(ref Target.subSystemList, Target.subSystemList.Length + 1);
                Target.subSystemList[^1] = system;

                PlayerLoop.SetPlayerLoop(Root);
            }

            public bool Remove<T>() => Remove(typeof(T));
            public bool Remove(Type type)
            {
                CacheList.Clear();
                CacheList.AddRange(Target.subSystemList);

                for (int i = CacheList.Count - 1; i >= 0; i--)
                {
                    if (CacheList[i].type == type)
                    {
                        CacheList.RemoveAt(i);
                        break;
                    }
                }

                if (CacheList.Count == Target.subSystemList.Length)
                    return false;

                Target.subSystemList = CacheList.ToArray();
                PlayerLoop.SetPlayerLoop(Root);
                return true;
            }

            public PlayerLoopContext(PlayerLoopSystem Root, int index)
            {
                this.Root = Root;
                this.Index = index;
            }

            static List<PlayerLoopSystem> CacheList = new(10);
        }
    }
}

public struct EarlyNetworkUpdate { }
public struct LateNetworkUpdate { }