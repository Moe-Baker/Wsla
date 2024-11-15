using System.Collections.Generic;

using Unity.Mathematics;

using UnityEngine;

using Wsla;
using Wsla.Serialization;
using Wsla.Unity;

using Random = UnityEngine.Random;

public partial class Player : NetworkBehaviour
{
    [SerializeField]
    NetworkVariable<int> Number;

    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.Entity.AssignTraitHandler<string>(ApplyTrait);

        //Network.Entity.OnSpawn += SpawnCallback;

        Network.Entity.OnReplicated += SpawnCallback;

        var timer = Network.API.Tick.Register(10);
        timer.OnTick += Tick;
        void Tick(NetworkTickInfo info)
        {
            Debug.Log($"Tick on Frame {Time.frameCount}, ID: {info.GetID(0)}");
        }
    }

    void ApplyTrait(string attribute)
    {
        NetworkLog.Trace($"Attribute is {attribute}");
    }

    void SpawnCallback()
    {
        NetworkLog.Info($"Player {Network.Entity.ID} Spawned");

        if (Network.Owner.IsLocal)
        {
            Network.RPC.Invoke(nameof(Call))
                .WritePayload(stream =>
                {
                    NetworkSerializer.WriteValue("Bye World", stream);
                })
                .SetChannel(16)
                .SetBufferMode()
                .Broadcast();

            Number.Change(Random.Range(100, 1000))
                .SetChannel(16)
                .Broadcast();
        }
    }

    [RPC]
    void Call(INetworkStream stream, RpcInfo info)
    {
        var text = NetworkSerializer.ReadValue<string>(stream);

        if (info.IsBuffered)
        {
            info.TryGetSender(out var sender);

            NetworkLog.Info($"Buffered RPC Called, Text: {text}, Sender: {sender}");
        }
        else
        {
            var sender = info.GetSender();
            NetworkLog.Info($"Realtime RPC Called, Text: {text}, Sender: {sender}");
        }
    }

    [RPC]
    void MultiCall(int a, int b, int c, int e, int f, int g, RpcInfo info)
    {
        RoomAPI.TransportProperty t = default;

        List<FixedString20> list = default;

        t.SendData(list);
    }

    [RPC]
    void Assume(List<string> list, RpcInfo info)
    {

    }
}

namespace N
{
    partial class A
    {
        partial class B
        {
            partial class C : NetworkBehaviour
            {
                NetworkVariable<float> A;

                NetworkVariable<int4x3> B;

                [RPC]
                void Call(int a, string b, RpcInfo info)
                {

                }
            }
        }
    }
}