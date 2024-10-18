using System;
using System.Buffers;
using System.Net;

using Cysharp.Threading.Tasks;

using MemoryPack;

using UnityEngine;

using Wsla.Shared.Global;
using Wsla.Unity;

public class Sandbox : MonoBehaviour
{
    NetworkAPI NetworkAPI => NetworkAPI.Instance;

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

        Debug.Log($"Connected to Room {response.Value}");
    }
}

public class SpanBufferWriter : IBufferWriter<byte>
{
    public static Span<byte> Serialize<T>(in T value, Span<byte> buffer, MemoryPackSerializerOptions options = null)
    {
        var state = MemoryPackWriterOptionalStatePool.Rent(options);

        SpanBufferWriter instance = default;

        var writer = new MemoryPackWriter<SpanBufferWriter>(ref instance, buffer, state);

        writer.WriteValue(in value);

        return buffer.Slice(0, writer.WrittenCount);
    }

    public void Advance(int count) => throw new NotImplementedException();
    public Memory<byte> GetMemory(int sizeHint = 0) => throw new NotSupportedException();
    public Span<byte> GetSpan(int sizeHint = 0) => throw new NotSupportedException();
}