using System.Net;
using System.Text.Json.Serialization;

using Cysharp.Threading.Tasks;

using LiteNetLib.Utils;

using Toolbox;

using UnityEngine;
using UnityEngine.UI;

using Wsla;
using Wsla.Unity;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        var data = new Data()
        {
            Number1 = 42,
            Number2 = 35
        };

        var json = System.Text.Json.JsonSerializer.Serialize(data);

        Debug.Log(json);

        return ButtonFieldOperation.None;
    });

    public struct Data
    {
        [JsonInclude]
        public int Number1 { get; set; }

        [JsonInclude]
        public int Number2 { get; set; }
    }
}