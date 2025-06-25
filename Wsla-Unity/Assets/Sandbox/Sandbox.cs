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
    public SyncAssetData SampleSyncData;

    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        //HashSet
        {
            var source = new HashSet<int>(new int[] { 1, 2, 3, 4, 5 });
            var clone = Duplicate(source);
        }

        //Queue
        {
            var source = new Queue<int>(new int[] { 1, 2, 3, 4, 5 });
            var clone = Duplicate(source);
        }

        //Stack
        {
            var source = new Stack<int>(new int[] { 1, 2, 3, 4, 5 });
            var clone = Duplicate(source);
        }

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

    [OptionalValueStyle(OptionalValueStyle.Inline)]
    public OptionalValue<float> Op1;

    public float Value;
}