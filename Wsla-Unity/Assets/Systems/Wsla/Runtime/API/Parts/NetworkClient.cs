using System.Collections.Generic;
using System.Text;

using LiteNetLib;

using Wsla.Serialization;

namespace Wsla.Unity
{
    public abstract class NetworkClient
    {
        public NetworkClientID ID { get; }
        public FixedString20 Username { get; private set; }

        /// <summary>
        /// Is this your local client? opposite of <see cref="IsRemote"/>
        /// </summary>
        public bool IsLocal => this is LocalNetworkClient;

        /// <summary>
        /// Is this NOT your local client? opposite of <see cref="IsLocal"/>
        /// </summary>
        public bool IsRemote => IsLocal is false;

        public bool IsMaster => Room.Clients.Master == this;

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

        public RoomAPI Room { get; }

        public static NetworkClientID ReadID(NetPacketReader reader)
        {
            return NetworkSerializer.ReadValue<NetworkClientID>(reader);
        }
        public virtual void ReadState(NetPacketReader reader)
        {
            Username = NetworkSerializer.ReadValue<FixedString20>(reader);
        }

        public override string ToString()
        {
            IEnumerable<string> Parts()
            {
                if (IsLocal)
                    yield return "Local";

                if (IsMaster)
                    yield return "Master";

                yield return $"ID {ID.Value}";
            }

            var builder = new StringBuilder();

            builder.Append("[ ");

            builder.AppendJoin(" | ", Parts());

            builder.Append(" ]");

            return builder.ToString();
        }

        public NetworkClient(RoomAPI Room, NetworkClientID ID)
        {
            this.Room = Room;
            this.ID = ID;

            Entities = new(0);
        }
    }

    public class RemoteNetworkClient : NetworkClient
    {
        public static RemoteNetworkClient ReadInstance(RoomAPI room, ref NetPacketReader reader)
        {
            var id = ReadID(reader);

            var client = new RemoteNetworkClient(room, id);

            client.ReadState(reader);

            return client;
        }

        public RemoteNetworkClient(RoomAPI Room, NetworkClientID ID) : base(Room, ID) { }
    }
    public class LocalNetworkClient : NetworkClient
    {
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

        internal void ReadSpawnTokens(NetPacketReader reader, ClientConnectionResponse message)
        {
            for (int i = 0; i < message.SpawnTokens; i++)
            {
                var token = NetworkSerializer.ReadValue<NetworkEntityID>(reader);
                AddSpawnToken(token);
            }
        }
        #endregion

        public LocalNetworkClient(RoomAPI Room, NetworkClientID ID, int SpawnTokenCapacity) : base(Room, ID)
        {
            SpawnTokens = new Queue<NetworkEntityID>(SpawnTokenCapacity);
        }
    }
}