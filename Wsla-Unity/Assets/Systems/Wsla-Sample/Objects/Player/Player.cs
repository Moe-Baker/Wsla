using System.Collections.Generic;

using UnityEngine;

using Wsla;
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

        Network.RPC.Invoke(nameof(Call))
            .SetArguments("Hello World")
            .SetChannel(0)
            .SetDelivery(RemoteSyncDelivery.Unreliable)
            .SetBufferMode()
            .Broadcast();

        if (Network.Owner.IsLocal)
        {
            Number.Change(Random.Range(100, 1000))
                .SetDelivery(RemoteSyncDelivery.Unreliable)
                .SetChannel(0)
                .Broadcast();
        }
    }

    [RPC]
    void Call(string text, RpcInfo info)
    {
        NetworkLog.Info($"RPC Called, Text: {text}, Sender: {info.Sender}, Buffered: {info.IsBuffered}");
    }
}

partial class Player : IRemoteSyncMembers
{
    void IRemoteSyncMembers.RegisterRPCs(List<BaseRpcBind> list)
    {
        list.Add(new RpcBind<string>(Call));
    }

    void IRemoteSyncMembers.RegisterVariables(List<NetworkVariable> list)
    {
        list.Add(Number ??= new NetworkVariable<int>(default));
    }
}