using LiteNetLib.Utils;

using Toolbox;

using UnityEngine;

using Wsla.Serialization;
using Wsla.Unity;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        var original = new Sample1<DataPayload2, DataPayload1>() { Data1 = new DataPayload2() { Data = 1 }, Data2 = new DataPayload1() { Data = 2 } };

        var clone = Duplicate(in original);

        Debug.Log($"{original.Data1.Data} : {clone.Data1.Data}");
        Debug.Log($"{original.Data2.Data} : {clone.Data2.Data}");

        return ButtonFieldOperation.None;
    });

    public static T Duplicate<[NetworkSerializationMarker] T>(in T original)
    {
        var writer = new NetDataWriter(true, 512);

        NetworkSerializer.WriteValue(in original, writer);

        var reader = new NetDataReader(writer);

        var clone = NetworkSerializer.ReadValue<T>(reader);

        Debug.Assert(reader.Position == writer.Length);

        return clone;
    }
}

public class Sample1<[NetworkSerializationMarker] T1, [NetworkSerializationMarker] T2> : IAutoNetworkSerialization
{
    public T1 Data1;
    public T2 Data2;

    public void Select(ref AutoSerializationContext context)
    {
        context.Select(ref Data1);
        context.Select(ref Data2);
    }
}

public class Sample2<T2> : Sample1<DataPayload1, T2> { }
public class Sample3 : Sample1<DataPayload1, DataPayload2> { }

public class DataPayload1 : IAutoNetworkSerialization
{
    public int Data;

    public void Select(ref AutoSerializationContext context)
    {
        context.Select(ref Data);
    }
}

public class DataPayload2 : IAutoNetworkSerialization
{
    public int Data;

    public void Select(ref AutoSerializationContext context)
    {
        context.Select(ref Data);
    }
}