using LiteNetLib;
using LiteNetLib.Utils;

using System;
using System.Collections.Generic;

using Toolbox;

using Wsla.Serialization;

namespace Wsla.Unity
{
    [Serializable]
    public abstract class NetworkVariable
    {
        public NetworkEntity.Behaviour Behaviour { get; private set; }
        public NetworkEntity Entity => Behaviour.Entity;

        public NetworkSyncMemberID ID { get; private set; }

        internal void Set(NetworkSyncMemberID ID, NetworkEntity.Behaviour Behaviour)
        {
            this.ID = ID;
            this.Behaviour = Behaviour;
        }

        internal abstract void Read(ref BinarySource source, NetworkVariableInfo info);
        internal abstract void Write(ref BinarySource source);
    }

    [Serializable]
    public class NetworkVariable<T> : NetworkVariable
    {
        T Value_Internal;
        public T Value => Value_Internal;

        public void Initialize(in T value, EntitySpawnTicket ticket)
        {
            ticket.WriteVariable(this, in value);
        }

        public VariableInvocationBuilder<T> Change(T value) => new VariableInvocationBuilder<T>(this, value);

        internal override void Read(ref BinarySource reader, NetworkVariableInfo info)
        {
            NetworkSerializer.ReadValue(ref Value_Internal, ref reader);
            Set(Value_Internal, info);
        }
        internal override void Write(ref BinarySource writer)
        {
            Write(in Value_Internal, ref writer);
        }

        internal void Write(in T value, ref BinarySource source)
        {
            NetworkSerializer.WriteValue(in value, ref source);
        }

        public event SetDelegate OnSet;
        public delegate void SetDelegate(ChangePairData<T> value, NetworkVariableInfo info);
        internal void Set(T target, NetworkVariableInfo info)
        {
            var change = new ChangePairData<T>(Value_Internal, target);
            Value_Internal = target;
            OnSet?.Invoke(change, info);
        }

        public NetworkVariable() { }
    }

    public interface IRegisterCustomVariables
    {
        void RegisterCustomVariables(List<NetworkVariable> list);
    }

    public struct NetworkVariableInfo : ISyncMemberInfo
    {
        NetworkClientID SenderID;

        public DeliveryMethod Delivery { get; }
        public byte Channel { get; }
        public bool IsBuffered { get; }

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

        /// <summary>
        /// Get the sender of the Variable, only valid if this Variable's Sender is still in the Room, consider using <see cref="TryGetSender(out NetworkClient)"/> for a safer alternative
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public NetworkClient GetSender()
        {
            if (TryGetSender(out var sender) is false)
                throw new InvalidOperationException($"Variable Sender Disconnected");

            return sender;
        }

        /// <summary>
        /// Tries to get the sender of the Variable
        /// </summary>
        /// <param name="client"></param>
        /// <returns>true if found, false if not</returns>
        /// <exception cref="Exception"></exception>
        public bool TryGetSender(out NetworkClient client)
        {
            if (SenderID == NetworkClientID.None)
            {
                client = default;
                return false;
            }

            if (Room.Clients.TryGet(SenderID, out client) is false)
                throw new Exception($"No Sender Found for Variable {SenderID}, a Replication Error, Please Report");

            return true;
        }

        public NetworkVariableInfo(NetworkClientID SenderID, byte Channel, DeliveryMethod Delivery, bool IsBuffered)
        {
            this.SenderID = SenderID;
            this.Channel = Channel;
            this.Delivery = Delivery;
            this.IsBuffered = IsBuffered;
        }

        public static NetworkVariableInfo FromRemote(ref NetworkVariableCommand command, byte channel, DeliveryMethod delivery)
        {
            return new NetworkVariableInfo(command.Sender, channel, delivery, false);
        }

        public static NetworkVariableInfo FromLocal<T>(ref VariableInvocationBuilder<T> builder)
        {
            var senderID = Room.Clients.Local.ID;

            return new NetworkVariableInfo(senderID, builder.Channel, builder.Delivery, false);
        }

        public static NetworkVariableInfo FromBuffer(NetworkClientID senderID) => new NetworkVariableInfo(senderID, 0, DeliveryMethod.ReliableOrdered, true);

        public static NetworkVariableInfo FromInitialization() => FromInitialization(Room.Clients.Local.ID);
        public static NetworkVariableInfo FromInitialization(NetworkClientID senderID) => new NetworkVariableInfo(senderID, 0, DeliveryMethod.ReliableOrdered, true);
    }

    public struct VariableInvocationBuilder<T>
    {
        internal readonly NetworkVariable<T> Variable;
        internal readonly T Value;

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

        internal byte Channel;
        public VariableInvocationBuilder<T> SetChannel(byte value)
        {
            Channel = value;
            return this;
        }
        public VariableInvocationBuilder<T> SetChannel(NetworkChannelField value)
        {
            Channel = value;
            return this;
        }

        internal DeliveryMethod Delivery;
        public VariableInvocationBuilder<T> SetDelivery(RemoteSyncDelivery value)
        {
            Delivery = (DeliveryMethod)value;
            return this;
        }

        NetworkSyncMemberParameters GetParameters() => new NetworkSyncMemberParameters(Variable.Entity.ID, Variable.Behaviour.ID, Variable.ID);

        void ValidateReplicationSettings()
        {
            if (Variable.Entity.IsReplicated is false)
            {
                if (Delivery is not DeliveryMethod.ReliableOrdered)
                {
                    Delivery = DeliveryMethod.ReliableOrdered;
                    NetworkLog.Warning($"Can only Set {Delivery} via {Variable.Entity} while it's not Replicated");
                }

                if (Channel is not 0)
                {
                    Channel = 0;
                    NetworkLog.Warning($"Can only Set on channel {Channel} via {Variable.Entity} while it's not Replicated");
                }
            }
        }

        /// <summary>
        /// Broadcasted to all clients and buffered for late joining clients
        /// </summary>
        public void Broadcast()
        {
            ValidateReplicationSettings();

            //Local
            {
                var info = NetworkVariableInfo.FromLocal(ref this);
                Variable.Set(Value, info);
            }

            //Remote
            {
                var writer = Room.Pools.SinglePackerWriter.Take();
                var source = BinarySource.From(writer);

                var parameters = GetParameters();
                var request = new BroadcastNetworkVariableRequest(parameters);

                NetworkSerializer.WriteHeader(in request, ref source);
                Variable.Write(ref source);

                Room.Transport.SendWriter(in writer, channel: Channel, delivery: Delivery);
            }
        }

        /// <summary>
        /// buffered for all late joining clients, but not broadcasted to currently joining clients
        /// </summary>
        public void Buffer()
        {
            ValidateReplicationSettings();

            //Local
            {
                var info = NetworkVariableInfo.FromLocal(ref this);
                Variable.Set(Value, info);
            }

            //Remote
            {
                var writer = Room.Pools.SinglePackerWriter.Take();
                var source = BinarySource.From(writer);

                var parameters = GetParameters();
                var request = new BufferNetworkVariableRequest(parameters);

                NetworkSerializer.WriteHeader(in request, ref source);
                Variable.Write(ref source);

                Room.Transport.SendWriter(in writer, channel: Channel, delivery: Delivery);
            }
        }

        public VariableInvocationBuilder(NetworkVariable<T> Variable, T Value)
        {
            this.Variable = Variable;
            this.Value = Value;

            Channel = 0;
            Delivery = DeliveryMethod.ReliableOrdered;
        }
    }
}