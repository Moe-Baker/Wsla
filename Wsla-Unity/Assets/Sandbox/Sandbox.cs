using System;
using System.Buffers;
using System.Net;

using Cysharp.Threading.Tasks;

using Toolbox;

using UnityEngine;

using Wsla.Serialization;
using Wsla.Shared.Global;
using Wsla.Unity;

public class Sandbox : MonoBehaviour
{
    NetworkAPI NetworkAPI => NetworkAPI.Instance;

    public ButtonField Execute = ButtonField.Create<Sandbox>(x =>
    {
        Debug.Log(NetworkGeneratedCode.Text);
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

        var response = await NetworkAPI.Room.Connect(IPAddress.Loopback, Constants.RelayManagementPort, request);
        if (response.IsError)
        {
            Debug.LogError($"Failed to Connect to Room, Error: {response.Error}");
            return;
        }

        var room = response.Value;
        Debug.Log($"Connected to Room {room}");

        await UniTask.Delay(TimeSpan.FromSeconds(1));

        //Load Scene
        {
            await room.Scenes.Load(new NetworkSceneID(1), NetworkSceneLoadMode.Single)
                .Add(new NetworkSceneID(2))
                .Send();

            Debug.Log("Scene Load Finished");
        }

        return;

        //Create Entity
        {
            room.Entities.Spawn()
                .SetResource(new NetworkEntityResource(0))
                .Send();
        }
    }
}

class Base<V>
{
    class Sample<T>
    {
        void Usage()
        {
            var sample = new Sample<IPAddress>();
            Definition(sample);
        }

        static void Definition<[NetworkSerializationMarker] T>(T value)
        {

        }
    }
}