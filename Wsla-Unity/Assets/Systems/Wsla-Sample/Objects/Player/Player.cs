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

        Network.Entity.OnSpawn += SpawnCallback;
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

    void Update()
    {
        if (Network.Entity.IsMine is false)
            return;

        transform.position += (Vector3)(GetInput() * 10 * Time.deltaTime);
    }

    Vector2 GetInput()
    {
        var input = Vector2.zero;

        if (Input.GetKey(KeyCode.W))
        {
            input.y += 1;
        }
        if (Input.GetKey(KeyCode.S))
        {
            input.y -= 1;
        }

        if (Input.GetKey(KeyCode.D))
        {
            input.x += 1;
        }
        if (Input.GetKey(KeyCode.A))
        {
            input.x -= 1;
        }

        return input.normalized;
    }
}

namespace N
{
    public partial class A
    {
        public partial class B
        {
            public partial class C : NetworkBehaviour
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

class Sample
{
    void Usage()
    {
        Consumer<A>();
    }

    struct A
    {
        string call;
    }

    void Consumer<[NetworkSerializationMarker] T>()
    {

    }
}