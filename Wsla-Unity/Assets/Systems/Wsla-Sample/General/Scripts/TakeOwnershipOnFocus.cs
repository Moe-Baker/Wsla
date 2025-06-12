using UnityEngine;

using Wsla.Unity;

public class TakeOwnershipOnFocus : MonoBehaviour
{
    NetworkEntity Entity;

    void Awake()
    {
        Entity = GetComponent<NetworkEntity>();
    }

    void OnApplicationFocus(bool focus)
    {
        if (focus is false)
            return;

        if (Entity.IsSpawned is false)
            return;

        if (Entity.Owner.IsLocal is true)
            return;

        Entity.TakeOwnership();
    }
}