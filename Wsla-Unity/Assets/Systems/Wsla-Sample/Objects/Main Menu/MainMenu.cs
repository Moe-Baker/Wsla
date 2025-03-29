using Cysharp.Threading.Tasks;

using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using Wsla;
using Wsla.Unity;

public class MainMenu : MonoBehaviour
{
    public Button CreateRoomButton;
    public Button FindRoomButton;

    [Space]

    public Button JoinRoomButton;
    public TMP_InputField JoinRoomCode;

    [Space]

    public Button FindMatchButton;

    NetworkAPI NetworkAPI => NetworkAPI.Instance;

    CanvasGroup CanvasGroup;

    void Start() => Initialize().Forget();

    async UniTaskVoid Initialize()
    {
        Application.runInBackground = true;

        CanvasGroup = gameObject.AddComponent<CanvasGroup>();

        CanvasGroup.interactable = false;
        {
            await NetworkAPI.Prepare();
        }
        CanvasGroup.interactable = true;

        CreateRoomButton.onClick.AddListener(() => PerformOperation(CreateRoomAction));
        FindRoomButton.onClick.AddListener(() => PerformOperation(FindRoomAction));
        JoinRoomButton.onClick.AddListener(() => PerformOperation(JoinRoomAction));
        FindMatchButton.onClick.AddListener(() => PerformOperation(FindMatchAction));
    }

    CreateRoomParameters GetCreateRoomParameters()
    {
        var Name = "SAMPLE-ROOM-NAME";
        var Capacity = (byte)3;
        var Scene = NetworkSceneID.From(1);
        var Password = new FixedString<FS20>();
        var Privacy = RoomPrivacy.Private;
        var Lock = RoomLockPolicy.AfterFill;

        return new CreateRoomParameters(Name, Capacity, Scene, Password, Privacy, Lock);
    }

    async void PerformOperation(Func<UniTask> operation)
    {
        CanvasGroup.interactable = false;

        try
        {
            await operation();
        }
        finally
        {
            CanvasGroup.interactable = true;
        }
    }

    async UniTask CreateRoomAction()
    {
        var request = GetCreateRoomParameters();
        var response = await NetworkAPI.MatchMaking.CreateRoom(ServerRegion.EU, request);

        if (response.IsError)
        {
            NetworkLog.Error($"Failed to Create Room, Error: {response.Error}");
            return;
        }

        await JoinRoom(response.Value);
    }
    async UniTask FindRoomAction()
    {
        var response = await NetworkAPI.MatchMaking.FindRoom(ServerRegion.EU, GetCreateRoomParameters());

        if (response.IsError)
        {
            NetworkLog.Error($"Failed to Find Room, Error: {response.Error}");
            return;
        }

        var info = response.Value;

        await JoinRoom(info);
    }
    async UniTask JoinRoomAction()
    {
        var code = JoinRoomCode.text;

        if (RoomConnectionInfo.TryParseCode(code, out var info) is false)
        {
            Debug.LogError($"Invalid Code");
            return;
        }

        await JoinRoom(info);
    }
    async UniTask FindMatchAction()
    {
        var response = await NetworkAPI.MatchMaking.FindMatch(ServerRegion.EU).Operate();

        if (response.IsError)
        {
            NetworkLog.Error($"Error on Find Match: {response.Error}");
            return;
        }

        await JoinRoom(response.Value);
    }

    public async UniTask JoinRoom(RoomConnectionInfo info)
    {
        var request = new ClientConnectionRequest("SAMPLE-USERNAME", "HELLO-WORLD");
        var response = await NetworkAPI.Room.Connect(info, request);

        if (response.IsError)
        {
            NetworkLog.Error($"Failed to Connect to Room, Error: {response.Error}");
            return;
        }

        NetworkLog.Trace($"Connected to Room {NetworkAPI.Room.ConnectionInfo}");
    }
}