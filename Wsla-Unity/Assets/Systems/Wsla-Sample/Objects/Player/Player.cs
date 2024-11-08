using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        Debug.LogWarning($"Attribute is {attribute}");
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
                .SetBufferMode()
                .Broadcast();

            Number.Change(Random.Range(100, 1000))
                .SetChannel(0)
                .Broadcast();

            Respawn();
            async void Respawn()
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), destroyCancellationToken);
                }
                catch (OperationCanceledException operation) when (operation.CancellationToken == destroyCancellationToken)
                {
                    return;
                }

                Network.Room.Entities.Despawn(Network.Entity);

                Level.Instance.SpawnPlayer();
            }
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