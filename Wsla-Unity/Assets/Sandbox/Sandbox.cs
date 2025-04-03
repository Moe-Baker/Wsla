using LiteNetLib.Utils;

using System;
using System.Net;

using Toolbox;

using UnityEngine;

using Wsla;
using Wsla.Serialization;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        RoomConnectionInfo? source = new RoomConnectionInfo(IPAddress.Loopback, 4550);

        var clone = Duplicate(source);

        Debug.Log(clone);

        return ButtonFieldOperation.None;
    });

    public static T Duplicate<[NetworkSerializationMarker] T>(T original)
    {
        var writer = new NetDataWriter(true, 512);

        NetworkSerializer.WriteValue(in original, writer);

        var reader = new NetDataReader(writer);

        var clone = NetworkSerializer.ReadValue<T>(reader);

        Debug.Assert(reader.Position == writer.Length);

        return clone;
    }
}