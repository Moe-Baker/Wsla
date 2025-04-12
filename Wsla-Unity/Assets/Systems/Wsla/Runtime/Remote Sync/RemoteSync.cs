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
        /// <inheritdoc cref="DeliveryMethod.Unreliable"/>
        /// </summary>
        Unreliable = DeliveryMethod.Unreliable,

        /// <summary>
        /// <inheritdoc cref="DeliveryMethod.ReliableUnordered"/>
        /// </summary>
        ReliableUnordered = DeliveryMethod.ReliableUnordered,

        /// <summary>
        /// <inheritdoc cref="DeliveryMethod.Sequenced"/>
        /// </summary>
        Sequenced = DeliveryMethod.Sequenced,

        /// <summary>
        /// <inheritdoc cref="DeliveryMethod.ReliableOrdered"/>
        /// </summary>
        ReliableOrdered = DeliveryMethod.ReliableOrdered,
    }

    public interface ISyncMemberInfo
    {
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