using System;
using System.Net;

using Cysharp.Threading.Tasks;

using Toolbox;

using UnityEngine;

using Wsla;
using Wsla.Unity;

public class Sandbox : MonoBehaviour
{
    NetworkAPI NetworkAPI => NetworkAPI.Instance;

    public ButtonField Execute = ButtonField.Create<Sandbox>(x =>
    {
        return ButtonFieldOperation.None;
    });

    void Start()
    {
        Application.runInBackground = true;

        Initialize().Forget();
    }

    async UniTask Initialize()
    {
        var request = new ClientConnectionRequest("SAMPLE-USERNAME");

        var address = IPAddress.Parse("10.0.0.10");

        var response = await NetworkAPI.Room.Connect(address, Constants.RelayManagementPort, request);
        if (response.IsError)
        {
            Debug.LogError($"Failed to Connect to Room, Error: {response.Error}");
            return;
        }

        var room = response.Value;
        Debug.Log($"Connected to Room {room}");

        await UniTask.Delay(TimeSpan.FromSeconds(1));

        //Load Scene
        //{
        //    await room.Scenes.Load(new NetworkSceneID(1), NetworkSceneLoadMode.Single)
        //        .Add(new NetworkSceneID(2))
        //        .Send();
        //
        //    Debug.Log("Scene Load Finished");
        //}

        //Create Entity
        {
            room.Entities.Spawn()
                .SetResource(new NetworkEntityResource(0))
                .Send();
        }
    }
}