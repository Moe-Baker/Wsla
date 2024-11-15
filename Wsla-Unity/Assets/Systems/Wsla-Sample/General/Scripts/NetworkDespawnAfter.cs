using Cysharp.Threading.Tasks;

using System;
using System.Threading;
using System.Threading.Tasks;

using Toolbox;

using UnityEngine;

using Wsla.Unity;

public partial class NetworkDespawnAfter : NetworkBehaviour
{
    [SerializeField]
    SerializedTimeSpan Duration = SerializedTimeSpan.FromSeconds(5);

    CancellationToken OnDestroyCancellationToken;

    void Awake()
    {
        OnDestroyCancellationToken = destroyCancellationToken;
    }

    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.OnSpawn += SpawnCallback;
    }

    void SpawnCallback()
    {
        if (Network.Room.Clients.Local.IsMaster)
            Procedure().Forget();

        async UniTaskVoid Procedure()
        {
            try
            {
                await UniTask.Delay(Duration.Span, cancellationToken: OnDestroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (OnDestroyCancellationToken.IsCancellationRequested)
                return;

            Network.Room.Entities.Despawn(Network.Entity);
        }
    }
}