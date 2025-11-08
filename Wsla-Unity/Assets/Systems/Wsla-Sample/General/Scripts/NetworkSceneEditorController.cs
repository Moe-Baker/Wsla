using UnityEngine;
using System.Collections.Generic;
using Toolbox;
using Wsla.Unity;
using Wsla;

public class NetworkSceneEditorController : MonoBehaviour
{
    [SerializeField]
    List<byte> Load;
    SparseArray<NetworkSceneID> GetLoadArray()
    {
        var array = SparseArray.Allocate<NetworkSceneID>(Load.Count);

        for (int i = 0; i < Load.Count; i++)
            array[i] = new NetworkSceneID(Load[i]);

        return array;
    }

    [SerializeField]
    List<byte> Unload;
    SparseArray<NetworkSceneID> GetUnloadArray()
    {
        var array = SparseArray.Allocate<NetworkSceneID>(Unload.Count);

        for (int i = 0; i < Unload.Count; i++)
            array[i] = new NetworkSceneID(Unload[i]);

        return array;
    }

    static NetworkAPI API => NetworkAPI.Instance;

    [SerializeField]
    ButtonField ChangeScenes = ButtonField.Create<NetworkSceneEditorController>(self =>
    {
        var load = self.GetLoadArray();

        API.Room.Scenes.Change(load);

        return ButtonFieldOperation.None;
    });

    [SerializeField]
    ButtonField ModifyScenes = ButtonField.Create<NetworkSceneEditorController>(self =>
    {
        var load = self.GetLoadArray();
        var unload = self.GetUnloadArray();

        API.Room.Scenes.Modify(unload, load);

        return ButtonFieldOperation.None;
    });

    [RuntimeInitializeOnLoadMethod]
    static void OnLoad()
    {
        var gameObject = new GameObject("Network Scene Editor Controller");
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<NetworkSceneEditorController>();
    }
}