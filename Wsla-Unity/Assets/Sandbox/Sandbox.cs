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
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        return ButtonFieldOperation.None;
    });
}