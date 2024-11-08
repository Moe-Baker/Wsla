using System;
using System.Threading.Tasks;

using Toolbox;

using UnityEngine;

using Wsla.Unity;

public class NetworkDespawnAfter : NetworkBehaviour
{
    [SerializeField]
    SerializedTimeSpan Duration = SerializedTimeSpan.FromSeconds(5);

    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.OnSpawn += SpawnCallback;
    }

    void SpawnCallback()
    {
        if (Network.Room.Clients.Local.IsMaster)
            Procedure();

        async void Procedure()
        {
            try
            {
                await Task.Delay(Duration.Span, destroyCancellationToken);
            }
            catch (OperationCanceledException operation) when (operation.CancellationToken == destroyCancellationToken)
            {
                return;
            }

            Network.Room.Entities.Despawn(Network.Entity);
        }
    }
}