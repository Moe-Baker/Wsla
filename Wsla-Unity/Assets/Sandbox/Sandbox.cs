using LiteNetLib.Utils;

using System;
using System.Net;
using System.Threading;

using Toolbox;

using UnityEngine;

using Wsla;
using Wsla.Serialization;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        return ButtonFieldOperation.None;
    });
}