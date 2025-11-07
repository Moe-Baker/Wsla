using LiteNetLib.Utils;

using System;

using Wsla.Serialization;

namespace Wsla.Server
{
    public class NetworkEntity : IDisposable
    {
        public NetworkEntityID ID { get; }
        public NetworkEntityOrigin Origin { get; }
        public NetworkResourceID Resource { get; }

        #region Owner
        public NetworkClient Owner { get; private set; }
        public int OwnerRegisteration;

        public void AssignOwner(NetworkClient target)
        {
            Owner = target;
        }
        public void TransferOwner(NetworkClient target)
        {
            AssignOwner(target);
        }
        #endregion

        public NetworkEntityAuthorityMode Authority { get; private set; }

        public NetworkEntityTransferToken TransferToken { get; internal set; }

        public NetworkEntityDefinition Definition => new(ID, Origin, Resource, Authority, Owner.ID, TransferToken);

        #region Remote Buffer
        internal RemoteSyncBufferCollection RpcBuffer;
        internal RemoteSyncBufferCollection VariableBuffer;
        #endregion

        public void Dispose()
        {
            RpcBuffer.Dispose();
            VariableBuffer.Dispose();
        }

        public override string ToString() => $"(ID: {ID})";

        readonly Room Room;
        public NetworkEntity(Room Room, NetworkEntityID ID, NetworkEntityOrigin Origin, NetworkResourceID Resource, NetworkClient Owner, NetworkEntityAuthorityMode Authority)
        {
            this.Room = Room;
            this.ID = ID;

            this.Origin = Origin;
            this.Resource = Resource;

            this.Owner = Owner;

            this.Authority = Authority;

            RpcBuffer = new(Room);
            VariableBuffer = new(Room);

            TransferToken = new NetworkEntityTransferToken(0);
        }
    }
}