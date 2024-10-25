using System.Collections.Generic;

using Wsla;
using Wsla.Unity;

public partial class Player : NetworkBehaviour
{
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
    }

    [RPC]
    void Call(string text, RpcInfo info)
    {
        NetworkLog.Info($"RPC Called By {info.Sender}");
    }
}

partial class Player : IRemoteSyncMembers
{
    void IRemoteSyncMembers.RegisterRPCs(List<BaseRpcBind> list)
    {
        list.Add(new RpcBind<string>(Call));
    }
}