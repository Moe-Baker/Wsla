using System.Text.Json;
using System.Text.Json.Serialization;

using Toolbox;

using UnityEngine;

using Wsla;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        //1
        {
            var data = new NetworkSceneID(14);

            var json = JsonSerializer.Serialize(data, options: SharedAPI.JsonOptions);

            Debug.Log(json);

            var data2 = JsonSerializer.Deserialize<NetworkSceneID>(json);

            Debug.Log(data2);
        }

        return ButtonFieldOperation.None;
    });

    public struct Data
    {
        public int Number1 { get; set; }
        public int Number2 { get; set; }
    }
}