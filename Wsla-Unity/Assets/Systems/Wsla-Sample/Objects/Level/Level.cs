using UnityEngine;

using Wsla.Unity;

public class Level : NetworkBehaviour
{
    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.Entity.OnSpawn += SpawnCallback;
    }

    void SpawnCallback()
    {
        Network.Room.Entities.Spawn()
            .SetResource(new Wsla.NetworkEntityResource(0))
            .SetAuthority(Wsla.NetworkEntityAuthorityMode.Explicit)
            .Send();
    }
}