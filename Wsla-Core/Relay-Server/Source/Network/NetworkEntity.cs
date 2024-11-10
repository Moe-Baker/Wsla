using LiteNetLib;
using LiteNetLib.Utils;

using Wsla.Serialization;

namespace Wsla.Server
{
    public class NetworkEntity : IDisposable
    {
        public NetworkEntityID ID { get; }
        public NetworkEntityOrigin Origin { get; }
        public NetworkEntityResource Resource { get; }

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

        #region Remote Buffer
        internal RemoteSyncBufferCollection<NetworkRpcID> RpcBuffer;
        internal RemoteSyncBufferCollection<NetworkVariableID> VariableBuffer;
        #endregion

        #region Trait
        public NetDataWriter? TraitWriter;
        public bool HasTrait => TraitWriter is not null;

        internal void AssignTrait(NetPacketReader reader, int length)
        {
            if (length is 0)
                return;

            TraitWriter = Room.Pools.MultiPackerWriter.Retrieve();

            //Copy over data
            {
                var source = reader.PopAvailableSpan();
                var destination = TraitWriter.PopSpan(source.Length);
                source.CopyTo(destination);
            }
        }
        #endregion

        public void WriteDefinition(NetDataWriter writer)
        {
            var definition = new NetworkEntityDefinition(ID, Origin, Resource, Authority, Owner.ID, TransferToken);
            NetworkSerializer.WriteValue(in definition, writer);

            WriteTrait(writer);
        }
        public void WriteTrait(NetDataWriter writer)
        {
            if (HasTrait)
            {
                var source = TraitWriter.PeekAllocatedSpan();
                var destination = writer.PopSpan(source.Length);
                source.CopyTo(destination);
            }
        }

        public void Dispose()
        {
            if (HasTrait)
                Room.Pools.MultiPackerWriter.Return(TraitWriter);

            RpcBuffer.Dispose();
            VariableBuffer.Dispose();
        }

        public override string ToString() => $"(ID: {ID})";

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

            TransferToken = new NetworkEntityTransferToken(0);
        }
    }
}