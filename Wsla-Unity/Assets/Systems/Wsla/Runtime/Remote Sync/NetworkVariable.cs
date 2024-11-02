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
        public T Value { get; private set; }

        public VariableInvocationBuilder Change(T value) => Behaviour.Variables.Set(this).SetValue(value);

        public event SetDelegate OnSet;
        public delegate void SetDelegate(ChangePairData<T> value, NetworkVariableInfo info);
        internal override void Set(INetworkStream reader, NetworkVariableInfo info)
        {
            var previous = Value;
            Value = NetworkSerializer.ReadValue<T>(reader);
            var current = Value;

            OnSet?.Invoke(new(previous, current), info);
        }

        public NetworkVariable(T initial)
        {
            Value = initial;
        }
    }

    public interface IRegisterCustomVariables
    {
        void RegisterVariables(List<NetworkVariable> list);
    }

    public struct NetworkVariableInfo
    {
        public NetworkClient Sender { get; }
        public DeliveryMethod Delivery { get; }
        public byte Channel { get; }

        public bool IsBuffered { get; }

        public NetworkVariableInfo(NetworkClient Sender, byte Channel, DeliveryMethod Delivery, bool IsBuffered)
        {
            this.Sender = Sender;
            this.Channel = Channel;
            this.Delivery = Delivery;
            this.IsBuffered = IsBuffered;
        }

        public static NetworkVariableInfo From(RoomInstance room, ref NetworkVariableCommand command, byte channel, DeliveryMethod delivery)
        {
            if (room.Clients.TryGet(command.Sender, out var sender) is false)
                NetworkLog.Warning($"No Sender Found for RPC {command}");

            return new NetworkVariableInfo(sender, channel, delivery, false);
        }

        public static NetworkVariableInfo From(ref VariableInvocationBuilder builder)
        {
            var sender = builder.Room.Clients.Local;

            return new NetworkVariableInfo(sender, builder.Channel, builder.Delivery, false);
        }

        public static NetworkVariableInfo Buffered() => new NetworkVariableInfo(null, 0, DeliveryMethod.ReliableOrdered, true);
    }

    public struct VariableInvocationBuilder
    {
        internal readonly NetworkVariable Variable;
        internal readonly RoomInstance Room;

        internal NetDataWriter ValueWriter;
        internal NetDataWriter PacketWriter;

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
                var source = ValueWriter.PeekAllocatedSpan();
                var destination = output.PopSpan(source.Length);
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
        /// Broadcasted to all clients and bufferd for late joining clients
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
        /// bufferd for all late joining clients, but not broadcasted to currently joining clients
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
            var info = NetworkVariableInfo.From(ref this);

            //Reset arguments writer to be read from
            var marker = ValueWriter.Length;
            ValueWriter.SetPosition(0);
            {
                Variable.Set(ValueWriter, info);
            }
            ValueWriter.SetPosition(marker);
        }

        public VariableInvocationBuilder(NetworkVariable Variable, RoomInstance Room)
        {
            this.Variable = Variable;
            this.Room = Room;

            ValueWriter = ValueWriterPool.Take();
            PacketWriter = Room.Pools.SinglePackerWriter.Take();

            Channel = 0;
            Delivery = DeliveryMethod.ReliableOrdered;
        }

        static SinglePacketWriter ValueWriterPool = SinglePacketWriter.Create(512);
    }
}