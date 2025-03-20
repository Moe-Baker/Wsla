using System.Text.Json.Serialization;

using Toolbox;

using UnityEngine;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        var data = new Data()
        {
            Number1 = 42,
            Number2 = 35
        };

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