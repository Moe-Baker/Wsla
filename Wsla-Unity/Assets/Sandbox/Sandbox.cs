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
        var collection = new int[] { 1, 2, 3, 4, 5 };

        Debug.Log(collection.FormatString());

        return ButtonFieldOperation.None;
    });
}