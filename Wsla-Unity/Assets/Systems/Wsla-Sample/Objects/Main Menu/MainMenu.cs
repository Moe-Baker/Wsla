using Cysharp.Threading.Tasks;

using UnityEngine;
using UnityEngine.UI;

using Wsla;
using Wsla.Unity;

public class MainMenu : MonoBehaviour
{
    public Button StartButton;

    NetworkAPI NetworkAPI => NetworkAPI.Instance;

    void Start() => Initialize().Forget();

    static bool IsApiInitialized;

    async UniTaskVoid Initialize()
    {
        Application.runInBackground = true;

        if (IsApiInitialized is false)
        {
            StartButton.interactable = false;
            {
                await NetworkAPI.Prepare();
            }
            StartButton.interactable = true;

            IsApiInitialized = true;
        }

        StartButton.onClick.AddListener(() => CreateRoom().Forget());
    }

    async UniTask CreateRoom()
    {
        RoomConnectionInfo ConnectionInfo;

        //Create Room
        {
            var request = new CreateRoomRequest("SAMPLE-ROOM-NAME", 10);
            var response = await NetworkAPI.MatchMaking.CreateRoom(ServerRegion.EU, request);

            if (response.IsError)
            {
                NetworkLog.Error($"Failed to Create Room, Error: {response.Error}");
                return;
            }

            ConnectionInfo = response.Value;
        }

        //Connect to Room
        {
            var request = new ClientConnectionRequest("SAMPLE-USERNAME");
            var response = await NetworkAPI.Room.Connect(ConnectionInfo, request);

            if (response.IsError)
            {
                NetworkLog.Error($"Failed to Connect to Room, Error: {response.Error}");
                return;
            }

            NetworkLog.Trace($"Connected to Room {NetworkAPI.Room}");
        }
    }
}