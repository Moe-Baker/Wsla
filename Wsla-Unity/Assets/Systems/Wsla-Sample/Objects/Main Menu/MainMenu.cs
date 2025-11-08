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
        Application.targetFrameRate = 60;

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
        var Scenes = SparseArray.From(NetworkSceneID.From(1), NetworkSceneID.From(3), NetworkSceneID.From(4));
        var Password = new FixedString<FS20>();
        var Privacy = RoomPrivacy.Public;
        var Lock = RoomLockPolicy.AfterFill;
        var Shutdown = RoomShutdownPolicy.OnMasterDisconnect;

        return new CreateRoomParameters(Name, Capacity, Scenes, Password, Privacy, Lock, Shutdown);
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
        var parameters = MatchMakingParameters.New(3)
            .Add("Level", Level)
            .Add("Role", Role)
            .Add("Honor", Honor);

        var pool = new FixedString<FS20>("mini-games");
        var Scenes = SparseArray.From(NetworkSceneID.From(1), NetworkSceneID.From(3), NetworkSceneID.From(4));
        var regions = SparseArray.From(ServerRegion.EU, ServerRegion.USA, ServerRegion.Asia);

        var response = await NetworkAPI.MatchMaking.FindMatch(pool, Scenes, regions, parameters: parameters).Operate();

        if (response.IsError)
        {
            NetworkLog.Error($"Error on Find Match: {response.Error}");
            return;
        }

        await JoinRoom(response.Value);
    }

    #region Match Making Paramaters
    static int Honor = 4;
    static string Role = "Hunter";
    static int Level = 10;

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(Vector3.one * 2);

        DrawIntField("Honor", ref Honor);
        DrawTextField("Role", ref Role);
        DrawIntField("Level", ref Level);
    }

    void DrawIntField(string title, ref int field)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(title);

        GUILayout.Space(10);

        var text = field.ToString();

        text = GUILayout.TextField(text);

        if (int.TryParse(text, out field) is false)
            field = 0;

        GUILayout.EndHorizontal();
    }
    void DrawTextField(string title, ref string field)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(title);

        GUILayout.Space(10);

        field = GUILayout.TextField(field);

        GUILayout.EndHorizontal();
    }
    #endregion

    public async UniTask JoinRoom(RoomConnectionInfo info)
    {
        var request = new ClientConnectionRequest("SAMPLE-USERNAME", "HELLO-WORLD", NetworkGroupCollection.Empty);
        var response = await NetworkAPI.Room.Connect(info, request);

        if (response.IsError)
        {
            NetworkLog.Error($"Failed to Connect to Room, Error: {response.Error}");
            return;
        }

        NetworkLog.Trace($"Connected to Room {NetworkAPI.Room.ConnectionInfo}");
    }
}