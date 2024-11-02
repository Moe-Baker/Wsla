using LiteNetLib.Utils;

using Wsla.Serialization;

namespace Wsla.Server
{
    public class NetworkEntity : IDisposable
    {
        public NetworkEntityID ID { get; }
        public NetworkEntityOrigin Origin { get; }
        public NetworkEntityResource Resource { get; }

        public NetworkClient Owner { get; private set; }
        public int OwnerRegisteration;

        public NetworkEntityAuthorityMode Authority { get; private set; }

        internal RemoteSyncBufferCollection<NetworkRpcID> RpcBuffer;
        internal RemoteSyncBufferCollection<NetworkVariableID> VariableBuffer;

        public void Dispose()
        {
            RpcBuffer.Dispose();
            VariableBuffer.Dispose();
        }

        public void AssignOwner(NetworkClient target)
        {
            Owner = target;
        }
        public void TransferOwner(NetworkClient target)
        {
            AssignOwner(target);
        }

        public void WriteDefinition(NetDataWriter writer)
        {
            var definition = new NetworkEntityDefinition(ID, Origin, Resource, Authority, Owner.ID);
            NetworkSerializer.WriteValue(in definition, writer);
        }

        readonly Room Room;
        public NetworkEntity(Room Room, NetworkEntityID ID, NetworkEntityOrigin Origin, NetworkEntityResource Resource, NetworkClient Owner, NetworkEntityAuthorityMode Authority)
        {
            this.Room = Room;
            this.ID = ID;

            this.Origin = Origin;
            this.Resource = Resource;

            this.Owner = Owner;

            this.Authority = Authority;

            RpcBuffer = new(Room);
            VariableBuffer = new(Room);
        }
    }
}