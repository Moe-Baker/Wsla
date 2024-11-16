using LiteNetLib;

using System.Collections.Generic;

namespace Wsla.Unity
{
    public interface IRemoteSyncMembers
    {
        void RegisterRPCs(List<BaseRpcBind> list);
        void RegisterVariables(List<NetworkVariable> list);
    }

    public enum RemoteSyncDelivery : byte
    {
        /// <summary>
        /// Unreliable. Packets can be dropped, can be duplicated, can arrive without order.
        /// </summary>
        Unreliable = DeliveryMethod.Unreliable,

        /// <summary>
        /// Reliable. Packets won't be dropped, won't be duplicated, can arrive without order.
        /// </summary>
        ReliableUnordered = DeliveryMethod.ReliableUnordered,

        /// <summary>
        /// Unreliable. Packets can be dropped, won't be duplicated, will arrive in order.
        /// </summary>
        Sequenced = DeliveryMethod.Sequenced,

        /// <summary>
        /// Reliable and ordered. Packets won't be dropped, won't be duplicated, will arrive in order.
        /// </summary>
        ReliableOrdered = DeliveryMethod.ReliableOrdered,

        /// <summary>
        /// Reliable only last packet. Packets can be dropped (except the last one), won't be duplicated, will arrive in order.
        /// Cannot be fragmented
        /// </summary>
        ReliableSequenced = DeliveryMethod.ReliableSequenced
    }

    public interface ISyncMemberInfo
    {
        RoomAPI Room { get; }

        DeliveryMethod Delivery { get; }

        byte Channel { get; }

        bool IsBuffered { get; }

        /// <summary>
        /// Get the sender of the SyncMember, only valid if this SyncMember's Sender is still in the Room, consider using <see cref="TryGetSender(out NetworkClient)"/> for a safer alternative
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        NetworkClient GetSender();

        /// <summary>
        /// Tries to get the sender of the SyncMember
        /// </summary>
        /// <param name="client"></param>
        /// <returns>true if found, false if not</returns>
        /// <exception cref="Exception"></exception>
        bool TryGetSender(out NetworkClient client);
    }
}