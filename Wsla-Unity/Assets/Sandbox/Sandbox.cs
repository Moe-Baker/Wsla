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

class Sample
{
    void Use()
    {
        Definition(new Example1());
        Definition(new Example2());
        Definition(new Example3());
        Definition(new Example4());
        Definition(new Example5());
        Definition(new Example6.Example7());
    }

    void Definition<[NetworkSerializationMarker] TValue>(TValue value)
    {

    }
}

[NetworkBlittable]
public struct Example1
{
    public int X, Y, Z;
}

public class Example2 : IManualNetworkSerialization
{
    public void Read<TStream>(ref TStream stream) where TStream : INetworkStream
    {
        throw new NotImplementedException();
    }
    public void Write<TStream>(ref TStream stream) where TStream : INetworkStream
    {
        throw new NotImplementedException();
    }
}

public class Example3 : Example2 { }

public class Example4 : IAutoNetworkSerialization
{
    public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context) where TStream : INetworkStream
    {
        throw new NotImplementedException();
    }
}

public class Example5 : Example4 { }

public class Example6 : Example5
{
    public class Example7 : Example5
    {

    }
}