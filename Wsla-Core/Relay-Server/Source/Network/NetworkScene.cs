using System;

namespace Wsla.Server
{
    public class NetworkScene
    {
        public NetworkSceneID ID { get; }
        public NetworkSceneVersion Version { get; }

        public bool IsSpawned { get; internal set; }

        public NetworkSceneState Definition => new(ID, Version, IsSpawned);

        readonly Room Room;
        public NetworkScene(Room Room, NetworkSceneID ID, NetworkSceneVersion Version)
        {
            this.Room = Room;

            this.ID = ID;
            this.Version = Version;

            IsSpawned = false;
        }
    }
}
