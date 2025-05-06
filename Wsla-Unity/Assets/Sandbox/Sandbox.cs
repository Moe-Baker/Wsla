using LiteNetLib.Utils;

using System;
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
        var stream = new NetDataWriter();

        NetworkSerializer.WriteValue(in self.SampleSyncData, stream);
        stream.SetPosition(0);

        var clone = NetworkSerializer.ReadValue<SyncAssetData>(stream);

        Debug.Log(clone);

        Debug.Assert(clone == self.SampleSyncData);

        return ButtonFieldOperation.None;
    });
}