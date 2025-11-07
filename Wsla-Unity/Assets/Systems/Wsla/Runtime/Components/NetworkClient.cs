using System.Collections.Generic;
using System.Text;

using LiteNetLib;

using Wsla.Serialization;

namespace Wsla.Unity
{
    public abstract class NetworkClient
    {
        public NetworkClientID ID { get; }
        public FixedString<FS20> Username { get; private set; }

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
            target.OwnerRegistration = Entities.Add(target);
        }
        public void UnregisterEntity(NetworkEntity target)
        {
            Entities.RemoveAt(target.OwnerRegistration);
        }
        #endregion

        public NetworkAPI API => NetworkAPI.Instance;
        public RoomAPI Room => API.Room;

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

        public NetworkClient(NetworkClientID ID, FixedString<FS20> Username)
        {
            this.ID = ID;
            this.Username = Username;

            Entities = new(0);
        }
    }

    public class RemoteNetworkClient : NetworkClient
    {
        public RemoteNetworkClient(NetworkClientID ID, FixedString<FS20> Username) : base(ID, Username) { }
        public RemoteNetworkClient(NetworkClientDefinition definition) : this(definition.ID, definition.Username) { }
    }
    public class LocalNetworkClient : NetworkClient
    {
        #region Spawn Tokens
        public Queue<NetworkEntityID> SpawnTokens { get; private set; }
        public byte SpawnAllowance => (byte)SpawnTokens.Count;

        public void AddSpawnToken(NetworkEntityID id) => SpawnTokens.Enqueue(id);
        public NetworkEntityID RemoveSpawnToken() => SpawnTokens.Dequeue();

        internal void ReadSpawnTokens(NetPacketReader stream, ClientConnectionResponse message)
        {
            using var reader = new ClientConnectionResponse.SpawnTokenPayload.Reader(stream, message.SpawnTokens);

            SpawnTokens = new Queue<NetworkEntityID>(reader.Count);

            for (int i = 0; i < message.SpawnTokens; i++)
            {
                var token = reader.Read();
                AddSpawnToken(token);
            }
        }
        #endregion

        public LocalNetworkClient(NetworkClientID ID, FixedString<FS20> Username) : base(ID, Username) { }
        public LocalNetworkClient(NetworkClientDefinition definition) : this(definition.ID, definition.Username) { }
    }
}