using Cysharp.Threading.Tasks;

using System;
using System.Threading.Tasks;

using Wsla.Unity;

public partial class TakeOwnershipIfNot : NetworkBehaviour
{
    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.Entity.OnSpawn += SpawnCallback;
    }

    void SpawnCallback()
    {
        Procedure().Forget();
    }

    async UniTaskVoid Procedure()
    {
        var cancellation = destroyCancellationToken;

        var duration = TimeSpan.FromSeconds(UnityEngine.Random.Range(10, 15));

        while (true)
        {
            try
            {
                var interval = TimeSpan.FromMilliseconds(UnityEngine.Random.Range(100, 500));

                duration -= interval;

                await UniTask.Delay(interval, cancellationToken: cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellation.IsCancellationRequested)
                return;

            if (Network.Entity.IsRemote)
                Network.API.Room.Entities.TakeOwnership(Network.Entity);

            if (duration <= TimeSpan.Zero)
                break;
        }
    }
}