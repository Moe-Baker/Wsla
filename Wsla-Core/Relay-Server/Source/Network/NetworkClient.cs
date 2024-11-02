using LiteNetLib;
using LiteNetLib.Utils;

using Wsla.Serialization;

namespace Wsla.Server
{
    public class NetworkClient : IDisposable
    {
        public NetworkClientID ID { get; }

        public FixedString20 Username { get; private set; }

        public NetPeer? Peer { get; private set; }
        internal void AssignPeer(NetPeer value)
        {
            this.Peer = value;
        }

        public bool IsMaster => Room.Clients.Master == this;

        #region Spawn Tokens
        public Queue<NetworkEntityID> SpawnTokens { get; }
        public byte SpawnAllowance => (byte)SpawnTokens.Count;

        public void AddSpawnToken(NetworkEntityID id)
        {
            SpawnTokens.Enqueue(id);
        }
        public NetworkEntityID RemoveSpawnToken()
        {
            return SpawnTokens.Dequeue();
        }

        public bool ValdiateSpawnToken(NetworkEntityID id)
        {
            if (SpawnTokens.TryPeek(out var registerd) is false)
                return false;

            if (registerd != id)
                return false;

            RemoveSpawnToken();
            return true;
        }

        public void WriteSpawnTokens(NetDataWriter writer)
        {
            foreach (var token in SpawnTokens)
                NetworkSerializer.WriteValue(token, writer);
        }
        #endregion

        #region Entities
        public ExpandList<NetworkEntity> Entities { get; }

        public void RegisterEntity(NetworkEntity target)
        {
            target.OwnerRegisteration = Entities.Add(target);
        }

        public void UnregisterEntity(NetworkEntity target)
        {
            Entities.RemoveAt(target.OwnerRegisteration);
        }
        #endregion

        public void WriteState(NetDataWriter writer)
        {
            NetworkSerializer.WriteValue(ID, writer);
            NetworkSerializer.WriteValue(Username, writer);
        }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public void Dispose()
        {

        }

        readonly Room Room;
        public NetworkClient(Room Room, NetworkClientID ID, FixedString20 Username, int SpawnTokenCapacity)
        {
            this.Room = Room;

            this.ID = ID;
            this.Username = Username;

            SpawnTokens = new Queue<NetworkEntityID>(SpawnTokenCapacity);

            Entities = new(0);
        }
    }
}