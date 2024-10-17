using MemoryPack;

using UnityEngine;

public class Sandbox : MonoBehaviour
{
    void Start()
    {
        var v = new Data { Text = string.Empty };

        var bin = MemoryPackSerializer.Serialize(v);
        Debug.Log($"Binary Length: {bin.Length}");
    }
}

[MemoryPackable]
public partial struct Data
{
    public string Text;
}