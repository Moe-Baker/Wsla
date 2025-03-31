using LiteNetLib.Utils;

using System.Text.Json;
using System.Text.Json.Serialization;

using Toolbox;

using UnityEngine;

using Wsla;
using Wsla.Serialization;
using Wsla.Unity;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        SharedAPI.JsonOptions.Converters.Add(new SparseArrayJsonConverter<int>());

        var data = SparseArray.Clone<int>(new int[] { 1, 2, 3, 4 });

        Debug.Log(JsonSerializer.Serialize(data, options: SharedAPI.JsonOptions));

        return ButtonFieldOperation.None;
    });
}