using Wsla.Unity;

public class TakeOwnershipIfNot : NetworkBehaviour
{
    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.Entity.OnSpawn += SpawnCallback;
    }

    void SpawnCallback()
    {
        if (Network.Entity.IsRemote)
            Network.API.Room.Entities.TakeOwnership(Network.Entity);
    }
}