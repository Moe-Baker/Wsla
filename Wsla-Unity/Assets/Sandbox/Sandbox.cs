using LiteNetLib.Utils;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

using Toolbox;

using UnityEngine;

using Wsla;
using Wsla.Serialization;
using Wsla.Unity;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
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