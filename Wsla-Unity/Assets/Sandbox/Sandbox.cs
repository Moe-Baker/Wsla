using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;

using Cysharp.Threading.Tasks;

using Toolbox;

using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;

using Wsla.Serialization;
using Wsla.Shared.Global;
using Wsla.Unity;

[assembly: NetworkSerializationResolverRegisteration(typeof(Sample), 0, "Register")]

public static class Sample
{
    public static void Register()
    {
        Debug.Log("hello world");
    }
}

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

        Wsla.Serialization.NetworkSerializer.Clone(new Data());


        return;

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

[NetworkBlittable]
struct Data
{
    public float X, Y, Z;
}