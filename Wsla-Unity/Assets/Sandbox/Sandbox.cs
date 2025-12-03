using Toolbox;

using UnityEngine;

public class Sandbox : MonoBehaviour
{
    public ButtonField Execute = ButtonField.Create<Sandbox>(self =>
    {
        return ButtonFieldOperation.None;
    });
}