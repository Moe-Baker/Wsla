using System;
using System.Reflection;
using System.Text.Json;

using Toolbox;

using UnityEngine;

using Wsla;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        return ButtonFieldOperation.None;
    });

    public struct Data
    {
        public FixedString<FS20> Text;
    }
}