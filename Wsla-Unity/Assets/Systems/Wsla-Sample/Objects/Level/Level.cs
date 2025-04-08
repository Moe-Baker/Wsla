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

using Random = UnityEngine.Random;

public partial class Level : NetworkBehaviour
{
    [PrefabOnly]
    public GameObject PlayerPrefab;

    public Button DisconnectButton;

    public Button CodeButton;
    public TMP_Text CodeLabel;

    public TMP_Text RoomTimeLabel;

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

    void Update()
    {
        if (NetworkAPI.Room.IsConnected)
            RoomTimeLabel.text = $"Time: {NetworkAPI.Room.Time.CalculateElapsed().TotalMilliseconds.ToString("N1")}";
        else
            RoomTimeLabel.text = "Disconnected";
    }

    void SpawnCallback()
    {
        SpawnPlayer();

        if (Network.Room.Clients.Local.IsMaster)
            SwapScene().Forget();
    }

    public void SpawnPlayer()
    {
        var ticket = Network.Room.Entities.Spawn()
            .SetPrefab(PlayerPrefab)
            .SetAuthority(NetworkEntityAuthorityMode.Explicit)
            .Ticket();

        //Set Position
        {
            var player = ticket.Entity.Behaviours.Get<Player>();

            var position = new Vector3()
            {
                x = Random.Range(-5, +5),
                y = 0,
                z = Random.Range(-5, +5),
            };
            var rotation = Quaternion.Euler(Vector3.up * Random.Range(0, 360f));

            player.NetworkTransform.Initialize(ticket, position: position, rotation: rotation);
        }

        ticket.Send();
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