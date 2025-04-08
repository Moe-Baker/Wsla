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
        new Thread(() =>
        {
            long ticks = DateTime.Now.Ticks;
            while (true)
            {
                if (ticks != DateTime.Now.Ticks)
                {
                    ticks = DateTime.Now.Ticks;
                    Debug.Log(ticks);
                }
                else
                {
                    Debug.Log("same");
                }
            }
        }).Start();

        return ButtonFieldOperation.None;
    });
}