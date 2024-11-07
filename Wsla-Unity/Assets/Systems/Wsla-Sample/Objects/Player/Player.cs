using System.Collections.Generic;

using UnityEngine;

using Wsla;
using Wsla.Serialization;
using Wsla.Unity;

public partial class Player : NetworkBehaviour
{
    [SerializeField]
    NetworkVariable<int> Number;

    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.Entity.OnSpawn += SpawnCallback;
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
                .SetChannel(0)
                .SetDelivery(RemoteSyncDelivery.Unreliable)
                .SetBufferMode()
                .Broadcast();

            Number.Change(Random.Range(100, 1000))
                .SetDelivery(RemoteSyncDelivery.Unreliable)
                .SetChannel(0)
                .Broadcast();
        }
    }

    [RPC]
    void Call(INetworkStream stream, RpcInfo info)
    {
        var text = NetworkSerializer.ReadValue<string>(stream);

        if (info.IsBuffered)
        {
            NetworkLog.Info($"Buffered RPC Called, Text: {text}");
        }
        else
        {
            var sender = info.GetSender();
            NetworkLog.Info($"Realtime RPC Called, Text: {text}, Sender: {sender}");
        }
    }
}

partial class Player : IRemoteSyncMembers
{
    void IRemoteSyncMembers.RegisterRPCs(List<BaseRpcBind> list)
    {
        list.Add(new StreamRpcBind(Call));
    }

    void IRemoteSyncMembers.RegisterVariables(List<NetworkVariable> list)
    {
        list.Add(Number ??= new NetworkVariable<int>(default));
    }
}