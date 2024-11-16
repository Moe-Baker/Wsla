using LiteNetLib;
using LiteNetLib.Utils;

using System;
using System.Collections.Generic;

using Wsla.Serialization;

namespace Wsla.Unity
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class RPCAttribute : Attribute { }

    public abstract class BaseRpcBind
    {
        public NetworkEntity.Behaviour Behaviour { get; private set; }
        public NetworkEntity Entity => Behaviour.Entity;

        public NetworkRpcID ID { get; private set; }

        internal void Set(NetworkRpcID ID, NetworkEntity.Behaviour Behaviour)
        {
            this.ID = ID;
            this.Behaviour = Behaviour;
        }

        internal abstract string GetName();

        internal abstract void Invoke(INetworkStream reader, RpcInfo info);
    }

    public abstract class BaseRpcBind<TMethod> : BaseRpcBind
        where TMethod : Delegate
    {
        public TMethod Method { get; }

        internal override string GetName() => Method.Method.Name;

        public BaseRpcBind(TMethod Method) : base()
        {
            this.Method = Method;
        }
    }

    public class RpcBind : BaseRpcBind<RpcDelegate>
    {
        internal override void Invoke(INetworkStream reader, RpcInfo info)
        {
            Method(info);
        }

        public RpcBind(RpcDelegate Method) : base(Method) { }
    }
    public delegate void RpcDelegate(RpcInfo info);

    public class StreamRpcBind : BaseRpcBind<RpcDelegate<INetworkStream>>
    {
        internal override void Invoke(INetworkStream reader, RpcInfo info)
        {
            Method.Invoke(reader, info);
        }

        public StreamRpcBind(RpcDelegate<INetworkStream> Method) : base(Method) { }
    }

    public class RpcBind<T1> : BaseRpcBind<RpcDelegate<T1>>
    {
        T1 arg1;

        internal override void Invoke(INetworkStream reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, reader);

            Method(arg1, info);
        }

        public RpcBind(RpcDelegate<T1> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1>(T1 arg1, RpcInfo info);

    public class RpcBind<T1, T2> : BaseRpcBind<RpcDelegate<T1, T2>>
    {
        T1 arg1;
        T2 arg2;

        internal override void Invoke(INetworkStream reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, reader);
            NetworkSerializer.ReadValue(ref arg2, reader);

            Method(arg1, arg2, info);
        }

        public RpcBind(RpcDelegate<T1, T2> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2>(T1 arg1, T2 arg2, RpcInfo info);

    public class RpcBind<T1, T2, T3> : BaseRpcBind<RpcDelegate<T1, T2, T3>>
    {
        T1 arg1;
        T2 arg2;
        T3 arg3;

        internal override void Invoke(INetworkStream reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, reader);
            NetworkSerializer.ReadValue(ref arg2, reader);
            NetworkSerializer.ReadValue(ref arg3, reader);

            Method(arg1, arg2, arg3, info);
        }

        public RpcBind(RpcDelegate<T1, T2, T3> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3, RpcInfo info);

    public class RpcBind<T1, T2, T3, T4> : BaseRpcBind<RpcDelegate<T1, T2, T3, T4>>
    {
        T1 arg1;
        T2 arg2;
        T3 arg3;
        T4 arg4;

        internal override void Invoke(INetworkStream reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, reader);
            NetworkSerializer.ReadValue(ref arg2, reader);
            NetworkSerializer.ReadValue(ref arg3, reader);
            NetworkSerializer.ReadValue(ref arg4, reader);

            Method(arg1, arg2, arg3, arg4, info);
        }

        public RpcBind(RpcDelegate<T1, T2, T3, T4> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, RpcInfo info);

    public class RpcBind<T1, T2, T3, T4, T5> : BaseRpcBind<RpcDelegate<T1, T2, T3, T4, T5>>
    {
        T1 arg1;
        T2 arg2;
        T3 arg3;
        T4 arg4;
        T5 arg5;

        internal override void Invoke(INetworkStream reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, reader);
            NetworkSerializer.ReadValue(ref arg2, reader);
            NetworkSerializer.ReadValue(ref arg3, reader);
            NetworkSerializer.ReadValue(ref arg4, reader);
            NetworkSerializer.ReadValue(ref arg5, reader);

            Method(arg1, arg2, arg3, arg4, arg5, info);
        }

        public RpcBind(RpcDelegate<T1, T2, T3, T4, T5> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, RpcInfo info);

    public class RpcBind<T1, T2, T3, T4, T5, T6> : BaseRpcBind<RpcDelegate<T1, T2, T3, T4, T5, T6>>
    {
        T1 arg1;
        T2 arg2;
        T3 arg3;
        T4 arg4;
        T5 arg5;
        T6 arg6;

        internal override void Invoke(INetworkStream reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, reader);
            NetworkSerializer.ReadValue(ref arg2, reader);
            NetworkSerializer.ReadValue(ref arg3, reader);
            NetworkSerializer.ReadValue(ref arg4, reader);
            NetworkSerializer.ReadValue(ref arg5, reader);
            NetworkSerializer.ReadValue(ref arg6, reader);

            Method(arg1, arg2, arg3, arg4, arg5, arg6, info);
        }

        public RpcBind(RpcDelegate<T1, T2, T3, T4, T5, T6> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2, T3, T4, T5, T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, RpcInfo info);

    public struct RpcInfo : ISyncMemberInfo
    {
        public RoomAPI Room { get; }

        NetworkClientID SenderID;

        public DeliveryMethod Delivery { get; }
        public byte Channel { get; }
        public bool IsBuffered { get; }

        public NetworkClient GetSender()
        {
            if (TryGetSender(out var sender) is false)
                throw new InvalidOperationException($"RPC Sender Disconnected");

            return sender;
        }
        public bool TryGetSender(out NetworkClient client)
        {
            if (SenderID == NetworkClientID.None)
            {
                client = default;
                return false;
            }

            if (Room.Clients.TryGet(SenderID, out client) is false)
                throw new Exception($"No Sender Found for RPC {SenderID}, a Replication Error, Please Report");

            return true;
        }

        public RpcInfo(RoomAPI Room, NetworkClientID SenderID, byte Channel, DeliveryMethod Delivery, bool IsBuffered)
        {
            this.Room = Room;
            this.SenderID = SenderID;
            this.Channel = Channel;
            this.Delivery = Delivery;
            this.IsBuffered = IsBuffered;
        }

        public static RpcInfo FromRemote(RoomAPI room, ref NetworkRpcCommand command, byte channel, DeliveryMethod delivery)
        {
            return new RpcInfo(room, command.Sender, channel, delivery, false);
        }
        public static RpcInfo FromLocal(ref RpcInvocationBuilder builder)
        {
            var senderID = builder.Room.Clients.Local.ID;

            return new RpcInfo(builder.Room, senderID, builder.Channel, builder.Delivery, false);
        }
        public static RpcInfo FromBuffer(RoomAPI room, NetworkClientID senderID) => new RpcInfo(room, senderID, 0, DeliveryMethod.ReliableOrdered, true);
    }

    public interface IRegisterCustomRPCs
    {
        void RegisterRPCs(List<BaseRpcBind> list);
    }

    public struct RpcInvocationBuilder
    {
        internal readonly BaseRpcBind Bind;
        internal readonly RoomAPI Room;

        internal NetDataWriter ArgumentsWriter;
        internal NetDataWriter PacketWriter;

        internal byte Channel;
        public RpcInvocationBuilder SetChannel(byte value)
        {
            Channel = value;
            return this;
        }

        internal DeliveryMethod Delivery;
        public RpcInvocationBuilder SetDelivery(RemoteSyncDelivery value)
        {
            Delivery = (DeliveryMethod)value;
            return this;
        }

        internal RemoteBufferMode BufferMode;
        public RpcInvocationBuilder SetBufferMode() => SetBufferMode(RemoteBufferMode.Buffer);
        public RpcInvocationBuilder SetBufferMode(RemoteBufferMode value)
        {
            BufferMode = value;
            return this;
        }

        public RpcInvocationBuilder Arguments<T1>(T1 arg1)
        {
            NetworkSerializer.WriteValue(in arg1, ArgumentsWriter);

            return this;
        }
        public RpcInvocationBuilder Arguments<T1, T2>(T1 arg1, T2 arg2)
        {
            NetworkSerializer.WriteValue(in arg1, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg2, ArgumentsWriter);

            return this;
        }
        public RpcInvocationBuilder Arguments<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3)
        {
            NetworkSerializer.WriteValue(in arg1, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg2, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg3, ArgumentsWriter);

            return this;
        }
        public RpcInvocationBuilder Arguments<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            NetworkSerializer.WriteValue(in arg1, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg2, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg3, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg4, ArgumentsWriter);

            return this;
        }
        public RpcInvocationBuilder Arguments<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            NetworkSerializer.WriteValue(in arg1, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg2, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg3, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg4, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg5, ArgumentsWriter);

            return this;
        }
        public RpcInvocationBuilder Arguments<T1, T2, T3, T4, T5, T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            NetworkSerializer.WriteValue(in arg1, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg2, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg3, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg4, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg5, ArgumentsWriter);
            NetworkSerializer.WriteValue(in arg6, ArgumentsWriter);

            return this;
        }

        public RpcInvocationBuilder WriteValue<T>(T value) => Arguments(value);
        public RpcInvocationBuilder WritePayload(Action<INetworkStream> action)
        {
            action(ArgumentsWriter);
            return this;
        }

        public RpcInvocationBuilder GetPayloadWriter(out INetworkStream writer)
        {
            writer = ArgumentsWriter;
            return this;
        }

        NetworkRpcParameters GetParameters() => new NetworkRpcParameters(Bind.Entity.ID, Bind.Behaviour.ID, Bind.ID);
        void WriteArguments(NetDataWriter output)
        {
            if (ArgumentsWriter.Length > 0)
            {
                var source = ArgumentsWriter.PeekAllocatedSpan();
                var destination = output.PopSpan(source.Length);
                source.CopyTo(destination);
            }
        }

        void ValidateReplicationSettings()
        {
            if (Bind.Entity.IsReplicated is false)
            {
                if (Delivery is not DeliveryMethod.ReliableOrdered)
                {
                    Delivery = DeliveryMethod.ReliableOrdered;
                    NetworkLog.Warning($"Can only Send {Delivery} via {Bind.Entity} while it's not Replicated");
                }

                if (Channel is not 0)
                {
                    Channel = 0;
                    NetworkLog.Warning($"Can only Send on channel {Channel} via {Bind.Entity} while it's not Replicated");
                }
            }
        }

        /// <summary>
        /// Broadcasted to all clients and possibly buffered for late joining clients
        /// </summary>
        public void Broadcast()
        {
            ValidateReplicationSettings();

            //Remote
            {
                var parameters = GetParameters();
                var request = new BroadcastNetworkRpcRequest(BufferMode, parameters);

                NetworkSerializer.WriteHeader(in request, PacketWriter);

                WriteArguments(PacketWriter);

                Room.Transport.SendWriter(in PacketWriter, channel: Channel, delivery: Delivery);
            }

            //Local
            InvokeLocal();
        }

        /// <summary>
        /// bufferd for all late joining clients, but not broadcasted to currently joining clients
        /// </summary>
        public void Buffer()
        {
            ValidateReplicationSettings();

            var parameters = GetParameters();
            var request = new BufferNetworkRpcRequest(BufferMode, parameters);

            NetworkSerializer.WriteHeader(in request, PacketWriter);

            WriteArguments(PacketWriter);

            Room.Transport.SendWriter(in PacketWriter, channel: Channel, delivery: Delivery);
        }

        /// <summary>
        /// Send to a specific client
        /// </summary>
        /// <param name="Client"></param>
        public void Target(NetworkClient Client) => Target(Client.ID);
        /// <summary>
        /// <inheritdoc cref="Target(NetworkClient)"/>
        /// </summary>
        /// <param name="Target"></param>
        public void Target(NetworkClientID Target)
        {
            ValidateReplicationSettings();

            if (BufferMode is not RemoteBufferMode.None)
                NetworkLog.Warning($"Target RPCs Cannot be Buffered, Assigned Buffering Mode will be Ignored");

            if (Target == Room.Clients.Local.ID)
            {
                InvokeLocal();
                return;
            }

            var parameters = GetParameters();
            var request = new TargetNetworkRpcRequest(Target, parameters);

            NetworkSerializer.WriteHeader(in request, PacketWriter);

            WriteArguments(PacketWriter);

            Room.Transport.SendWriter(in PacketWriter, channel: Channel, delivery: Delivery);
        }

        void InvokeLocal()
        {
            var info = RpcInfo.FromLocal(ref this);

            //Reset arguments writer to be read from
            ArgumentsWriter.SetPosition(0);

            Bind.Invoke(ArgumentsWriter, info);
        }

        public RpcInvocationBuilder(BaseRpcBind Bind, RoomAPI Room)
        {
            this.Bind = Bind;
            this.Room = Room;

            ArgumentsWriter = ArgumentWriterPool.Take();
            PacketWriter = Room.Pools.SinglePackerWriter.Take();

            Channel = 0;
            Delivery = DeliveryMethod.ReliableOrdered;
            BufferMode = RemoteBufferMode.None;
        }

        static SinglePacketWriter ArgumentWriterPool = SinglePacketWriter.Create(512);
    }
}