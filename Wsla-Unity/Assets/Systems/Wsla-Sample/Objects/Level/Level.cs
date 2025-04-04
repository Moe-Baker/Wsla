using Cysharp.Threading.Tasks;

using System;
using System.Threading;

using TMPro;

using Toolbox;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Wsla;
using Wsla.Unity;

public partial class Level : NetworkBehaviour
{
    [PrefabOnly]
    public GameObject PlayerPrefab;

    public Button DisconnectButton;

    public Button CodeButton;
    public TMP_Text CodeLabel;

    public static Level Instance { get; private set; }

    NetworkAPI NetworkAPI => NetworkAPI.Instance;

    CancellationToken OnDestroyCancellationToken;

    void Awake()
    {
        Instance = this;

        if (NetworkAPI.Room.IsConnected is false)
            throw new InvalidOperationException($"Scene {gameObject.scene.name} Should Only be Loaded When Connected to a Room");

        OnDestroyCancellationToken = destroyCancellationToken;

        DisconnectButton.onClick.AddListener(DisconnectAction);

        //Setup Code
        {
            var code = NetworkAPI.Room.ConnectionInfo.GetCode();

            CodeLabel.text = code;
            CodeButton.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = code;
            });
        }
    }

    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.Entity.OnSpawn += SpawnCallback;
    }

    void SpawnCallback()
    {
        SpawnPlayer();

        if (Network.Room.Clients.Local.IsMaster)
            SwapScene().Forget();
    }

    public void SpawnPlayer()
    {
        var entity = Network.Room.Entities.InstantiatePrefab(PlayerPrefab);

        Network.Room.Entities.Spawn()
            .SetInstance(entity)
            .SetAuthority(NetworkEntityAuthorityMode.Explicit)
            .SetAuthority(NetworkEntityAuthorityMode.Transferable)
            .Send();
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

    void DisconnectAction()
    {
        Network.API.Room.Disconnect();
        SceneManager.LoadScene("Main Menu");
    }
}