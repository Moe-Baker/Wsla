using Cysharp.Threading.Tasks;

using UnityEngine;
using UnityEngine.UI;

using Wsla;
using Wsla.Unity;

public class MainMenu : MonoBehaviour
{
    public Button CreateMatchButton;
    public Button JoinMatchButton;

    NetworkAPI NetworkAPI => NetworkAPI.Instance;

    void Start() => Initialize().Forget();

    async UniTaskVoid Initialize()
    {
        Application.runInBackground = true;

        if (NetworkAPI.IsPrepared is false)
        {
            CreateMatchButton.interactable = false;
            {
                await NetworkAPI.Prepare();
            }
            CreateMatchButton.interactable = true;
        }

        CreateMatchButton.onClick.AddListener(() => CreateRoom().Forget());
        JoinMatchButton.onClick.AddListener(() => FindRoom().Forget());
    }

    CreateRoomParameters GetCreateRoomParameters() => new CreateRoomParameters("SAMPLE-ROOM-NAME", 10, NetworkSceneID.From(1), "HELLO-WORLD");

    async UniTask CreateRoom()
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

    async UniTask FindRoom()
    {
        var response = await NetworkAPI.MatchMaking.FindRoom(ServerRegion.EU, GetCreateRoomParameters());

        if (response.IsError)
        {
            NetworkLog.Error($"Failed to List Room, Error: {response.Error}");
            return;
        }

        var info = response.Value;

        if (info.HasValue is false)
        {
            NetworkLog.Error($"Zero Rooms Found");
            return;
        }

        await JoinRoom(info.Value);
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

        NetworkLog.Trace($"Connected to Room {NetworkAPI.Room}");
    }
}