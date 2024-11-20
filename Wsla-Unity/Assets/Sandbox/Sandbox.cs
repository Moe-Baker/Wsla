using System.Net;

using Cysharp.Threading.Tasks;

using LiteNetLib.Utils;

using Toolbox;

using UnityEngine;
using UnityEngine.UI;

using Wsla;
using Wsla.Unity;

public class Sandbox : MonoBehaviour
{
    public NetworkChannelField Field;

    public OptionalValue<FloatQuantizationParameters> quant1;
    public OptionalValue<IntegerQuantizationParameters> quant2;

    public Button StartButton;

    NetworkAPI NetworkAPI => NetworkAPI.Instance;

    void Start()
    {
        Application.runInBackground = true;

        StartButton.onClick.AddListener(() => Initialize().Forget());
    }

    async UniTask Initialize()
    {
        var request = new ClientConnectionRequest("SAMPLE-USERNAME");

        var address = IPAddress.Parse("10.0.0.10");

        var response = await NetworkAPI.Room.Connect(address, Constants.RelayManagementPort, request);
        if (response.IsError)
        {
            NetworkLog.Error($"Failed to Connect to Room, Error: {response.Error}");
            return;
        }

        NetworkLog.Trace($"Connected to Room {NetworkAPI.Room}");
    }

    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        var stream = new NetDataWriter(true, 64);

        Quantize.Float.Serialize(stream, 1.5f, self.quant1.Value);
        Quantize.Integer.Serialize(stream, 1234567, self.quant2.Value);

        stream.Position = 0;

        var val1 = Quantize.Float.Deserialize(stream, self.quant1.Value);
        Debug.Log($"Val-1 = {val1}");

        var val2 = Quantize.Integer.Deserialize(stream, self.quant2.Value);
        Debug.Log($"Val-2 = {val2}");

        return ButtonFieldOperation.None;
    });
}