using Cysharp.Threading.Tasks;

using System;
using System.Threading;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Wsla;
using Wsla.Unity;

public partial class Level : NetworkBehaviour
{
    public Button DisconnectButton;

    public static Level Instance { get; private set; }

    CancellationToken OnDestroyCancellationToken;

    void Awake()
    {
        Instance = this;

        OnDestroyCancellationToken = destroyCancellationToken;

        DisconnectButton.onClick.AddListener(DisconnectAction);
    }

    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.Entity.OnSpawn += SpawnCallback;
    }

    void SpawnCallback()
    {
        SpawnPlayers().Forget();

        if (Network.Room.Clients.Local.IsMaster)
            SwapScene().Forget();
    }

    async UniTaskVoid SwapScene()
    {
        return;

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(6), cancellationToken: OnDestroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (OnDestroyCancellationToken.IsCancellationRequested)
            return;

        var scene = gameObject.scene.buildIndex;

        if (scene is 1)
            scene = 2;
        else
            scene = 1;

        Network.Room.Scene.Change(new NetworkSceneID((byte)scene));
    }

    public async UniTaskVoid SpawnPlayers()
    {
        while (true)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(500), cancellationToken: OnDestroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (OnDestroyCancellationToken.IsCancellationRequested)
                return;

            Network.Room.Entities.Spawn()
                .SetResource(new NetworkEntityResource(0))
                .SetAuthority(NetworkEntityAuthorityMode.Explicit)
                .WriteTrait(new Vector3(UnityEngine.Random.Range(-5, 5), 0, UnityEngine.Random.Range(-5, 5)))
                .SetAuthority(NetworkEntityAuthorityMode.Transferable)
                .Send();

            break;
        }
    }

    void DisconnectAction()
    {
        Network.API.Room.Disconnect();
        SceneManager.LoadScene("Main Menu");
    }
}

static class TaskExtensions
{
    static TaskExtensions()
    {

    }
}