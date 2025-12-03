using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

using LiteNetLib.Utils;

using Toolbox;

using UnityEngine;

using Wsla;
using Wsla.Serialization;
using Wsla.Unity;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        var data = new Data()
        {
            A = 1,
            B = 2,
            C = 3,
        };

        self.SerializeArray(data);

        return ButtonFieldOperation.None;
    });

    public class Data : IAutoNetworkSerialization
    {
        public int A, B, C;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref A);
            context.Select(ref B);
            context.Select(ref C);
        }
    }

    public static TDuplicate Duplicate<[NetworkSerializationMarker] TDuplicate>(TDuplicate original)
    {
        var writer = new NetDataWriter(true, 512);


        NetworkSerializer.WriteValue(in original, writer);

        var reader = new NetDataReader(writer);

        var clone = NetworkSerializer.ReadValue<TDuplicate>(reader);

        Debug.Assert(reader.Position == writer.Length);

        return clone;
    }

    void SerializeArray<[NetworkSerializationMarker] TSerializeArray>(TSerializeArray item)
    {
        var array = new TSerializeArray[] { item };

        Duplicate<TSerializeArray[]>(array);
        Duplicate<List<TSerializeArray>>(default);
    }
}