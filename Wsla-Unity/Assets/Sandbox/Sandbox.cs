using System;

using Toolbox;

using UnityEngine;

using Wsla;
using Wsla.Serialization;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        var source = BinarySource.From(stackalloc byte[200]);

        var request = new CreateRoomRequest()
        {
            Application = "My App",
            Regions = (ServerRegion.Asia, ServerRegion.EU, ServerRegion.USA),
            Parameters = new CreateRoomParameters()
            {
                Name = "My Room",
                Capacity = 10,
                Scene = NetworkSceneID.From(1),
                Password = "Hello Password",
                Privacy = RoomPrivacy.Private,
                Lock = RoomLockPolicy.AfterFill,
            }
        };

        NetworkSerializer.WriteValue(request, ref source);

        source.Position = 0;

        var clone = NetworkSerializer.ReadValue<CreateRoomRequest>(ref source);

        return ButtonFieldOperation.None;
    });
}