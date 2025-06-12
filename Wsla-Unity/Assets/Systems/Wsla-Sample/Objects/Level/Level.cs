using System;

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
    RoomAPI Room => NetworkAPI.Room;

    void Awake()
    {
        Instance = this;

        if (NetworkAPI.Room.IsConnected is false)
            throw new InvalidOperationException($"Scene {gameObject.scene.name} Should Only be Loaded When Connected to a Room");

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

    void Update()
    {
        if (NetworkAPI.Room.IsConnected)
            RoomTimeLabel.text = $"Time: {NetworkAPI.Room.Time.CalculateElapsed().TotalMilliseconds.ToString("N1")}";
        else
            RoomTimeLabel.text = "Disconnected";
    }

    void OnGUI()
    {
        if (Room.IsConnected is false)
            return;

        var group = new NetworkGroupID(0);

        if (Room.Clients.Groups.Contains(group))
        {
            if (GUILayout.Button("Leave Group"))
            {
                Room.Clients.LeaveGroups(group);
            }
        }
        else
        {
            if (GUILayout.Button("Join Group"))
            {
                Room.Clients.JoinGroups(group);
            }
        }
    }

    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.Entity.OnSpawn += SpawnCallback;

        Room.Clients.OnDisconnect += ClientDisconnectCallback;
        Room.Clients.OnConnect += ClientConnectCallback;
        Room.Clients.OnChangeMaster += MasterChangeCallback;

        Room.OnDisconnect += RoomDisconnectCallback;
    }

    void SpawnCallback()
    {
        SpawnPlayer();
    }

    void RoomDisconnectCallback(LiteNetLib.DisconnectReason reason)
    {
        NetworkLog.Info($"Room Disconnected, Reason: {reason}");
    }
    void MasterChangeCallback(ChangePairData<NetworkClient> client)
    {
        NetworkLog.Info($"Master Client Changed from {client.Previous} to {client.Current}");
    }
    void ClientConnectCallback(NetworkClient client)
    {
        NetworkLog.Info($"Client {client} Connected");
    }
    void ClientDisconnectCallback(NetworkClient client)
    {
        NetworkLog.Info($"Client {client} Disconnected");
    }

    public void SpawnPlayer()
    {
        return;

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

        var entity = ticket.Send();

        entity.OutputGroups = new NetworkGroupID(0);
    }

    void DisconnectAction()
    {
        Network.API.Room.Disconnect();
        SceneManager.LoadScene("Main Menu");
    }
}