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

        public NetworkVariableID ID { get; private set; }

        internal void Set(NetworkVariableID ID, NetworkEntity.Behaviour Behaviour)
        {
            this.ID = ID;
            this.Behaviour = Behaviour;
        }

        internal abstract void Set(INetworkStream reader, NetworkVariableInfo info);
    }

    [Serializable]
    public class NetworkVariable<T> : NetworkVariable
    {
        T Value_Internal;
        public T Value => Value_Internal;

        public VariableInvocationBuilder Change(T value) => Behaviour.Variables.Set(this).SetValue(value);

        public void Initialize(T value)
        {
            Value_Internal = value;
            throw new NotImplementedException();
        }

        public event SetDelegate OnSet;
        public delegate void SetDelegate(ChangePairData<T> value, NetworkVariableInfo info);
        internal override void Set(INetworkStream reader, NetworkVariableInfo info)
        {
            var previous = Value_Internal;
            NetworkSerializer.ReadValue(ref Value_Internal, reader);
            var current = Value_Internal;

            OnSet?.Invoke(new(previous, current), info);
        }

        public NetworkVariable() : this(default) { }
        public NetworkVariable(T initial)
        {
            Value_Internal = initial;
        }
    }

    public interface IRegisterCustomVariables
    {
        void RegisterVariables(List<NetworkVariable> list);
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

        public static NetworkVariableInfo FromLocal(ref VariableInvocationBuilder builder)
        {
            var senderID = Room.Clients.Local.ID;

            return new NetworkVariableInfo(senderID, builder.Channel, builder.Delivery, false);
        }

        public static NetworkVariableInfo FromBuffer(NetworkClientID senderID) => new NetworkVariableInfo(senderID, 0, DeliveryMethod.ReliableOrdered, true);
    }

    public struct VariableInvocationBuilder
    {
        internal readonly NetworkVariable Variable;

        internal NetDataWriter ValueWriter;
        internal NetDataWriter PacketWriter;

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

        internal byte Channel;
        public VariableInvocationBuilder SetChannel(byte value)
        {
            Channel = value;
            return this;
        }

        internal DeliveryMethod Delivery;
        public VariableInvocationBuilder SetDelivery(RemoteSyncDelivery value)
        {
            Delivery = (DeliveryMethod)value;
            return this;
        }

        public VariableInvocationBuilder SetValue<T>(T value)
        {
            NetworkSerializer.WriteValue(in value, ValueWriter);

            return this;
        }

        NetworkVariableParameters GetParameters() => new NetworkVariableParameters(Variable.Entity.ID, Variable.Behaviour.ID, Variable.ID);
        void WriteValue(NetDataWriter output)
        {
            if (ValueWriter.Length > 0)
            {
                var source = ValueWriter.PeekAllocatedMemory().Span;
                var destination = output.PopMemory(source.Length).Span;
                source.CopyTo(destination);
            }
        }

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

            //Remote
            {
                var parameters = GetParameters();
                var request = new BroadcastNetworkVariableRequest(parameters);

                NetworkSerializer.WriteHeader(in request, PacketWriter);

                WriteValue(PacketWriter);

                Room.Transport.SendWriter(in PacketWriter, channel: Channel, delivery: Delivery);
            }

            //Local
            SetLocal();
        }

        /// <summary>
        /// buffered for all late joining clients, but not broadcasted to currently joining clients
        /// </summary>
        public void Buffer()
        {
            ValidateReplicationSettings();

            var parameters = GetParameters();
            var request = new BufferNetworkVariableRequest(parameters);

            NetworkSerializer.WriteHeader(in request, PacketWriter);

            WriteValue(PacketWriter);

            Room.Transport.SendWriter(in PacketWriter, channel: Channel, delivery: Delivery);
        }

        void SetLocal()
        {
            var info = NetworkVariableInfo.FromLocal(ref this);

            //Reset arguments writer to be read from
            var marker = ValueWriter.Length;
            ValueWriter.SetPosition(0);
            {
                Variable.Set(ValueWriter, info);
            }
            ValueWriter.SetPosition(marker);
        }

        public VariableInvocationBuilder(NetworkVariable Variable)
        {
            this.Variable = Variable;

            ValueWriter = ValueWriterPool.Take();
            PacketWriter = Room.Pools.SinglePackerWriter.Take();

            Channel = 0;
            Delivery = DeliveryMethod.ReliableOrdered;
        }

        static SinglePacketWriter ValueWriterPool = SinglePacketWriter.Create(512);
    }
}