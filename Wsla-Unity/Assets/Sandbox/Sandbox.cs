using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using Toolbox;

using UnityEngine;

using Wsla;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        var data = new FixedString20("Hello World");

        JsonClone(data);

        return ButtonFieldOperation.None;
    });

    public struct Data
    {
        public FixedString20 Text;
    }

    public static T JsonClone<T>(T original) => JsonClone(original, x => x.ToString());
    public static T JsonClone<T>(T original, Func<T, string> reader)
    {
        Debug.Log($"Original: [{reader(original)}]");

        var json = JsonSerializer.Serialize(original, options: SharedAPI.JsonOptions);

        Debug.Log($"Json: [{json}]");

        var clone = JsonSerializer.Deserialize<T>(json, options: SharedAPI.JsonOptions);

        Debug.Log($"Clone: [{reader(clone)}]");

        return clone;
    }
}