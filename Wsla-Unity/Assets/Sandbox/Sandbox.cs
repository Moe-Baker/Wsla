using LiteNetLib.Utils;

using System;
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
        var collection = new AttributeCollection(10);

        collection.SetValue("var1", 1);
        collection.SetValue("var2", 2);
        collection.SetValue("var3", 3);
        collection.SetValue("var4", DateTime.Now);
        collection.SetValue("var5", TimeSpan.FromSeconds(2.5));
        collection.SetValue("var6", Guid.NewGuid());

        var json = JsonSerializer.Serialize(collection, SharedAPI.JsonOptions);

        Debug.Log(json);

        var clone = JsonSerializer.Deserialize<AttributeCollection>(json, SharedAPI.JsonOptions);

        Debug.Log(clone.Dictionary.FormatString());

        return ButtonFieldOperation.None;
    });
}