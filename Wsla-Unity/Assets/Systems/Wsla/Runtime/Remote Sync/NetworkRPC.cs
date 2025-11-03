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

        public NetworkSyncMemberID ID { get; private set; }

        internal void Set(NetworkSyncMemberID ID, NetworkEntity.Behaviour Behaviour)
        {
            this.ID = ID;
            this.Behaviour = Behaviour;
        }

        internal abstract string GetName();

        internal void Invoke(INetworkStream stream, RpcInfo info)
        {
            var source = BinarySource.From(stream);
            Invoke(ref source, info);
        }
        internal abstract void Invoke(ref BinarySource reader, RpcInfo info);
    }

    public interface IBaseRpcBind<TParameters>
    {
        void Invoke(TParameters parameters, RpcInfo info);
    }
    public abstract class BaseRpcBind<TMethod, TParameters> : BaseRpcBind, IBaseRpcBind<TParameters>
        where TMethod : Delegate
        where TParameters : IRpcParameters
    {
        public TMethod Method { get; }

        internal override string GetName() => Method.Method.Name;

        void IBaseRpcBind<TParameters>.Invoke(TParameters parameters, RpcInfo info) => Invoke(parameters, info);
        internal abstract void Invoke(TParameters parameters, RpcInfo info);

        public BaseRpcBind(TMethod Method) : base()
        {
            this.Method = Method;
        }
    }

    public interface IRpcParameters
    {
        void WriteTo(INetworkStream stream);
    }

    public class RpcBind : BaseRpcBind<RpcDelegate, RpcParameters>
    {
        internal override void Invoke(ref BinarySource reader, RpcInfo info)
        {
            Method(info);
        }
        internal override void Invoke(RpcParameters parameters, RpcInfo info)
        {
            Method(info);
        }

        public RpcInvocationBuilder<RpcBind, RpcParameters> Invoke()
        {
            var parameters = new RpcParameters();

            return new RpcInvocationBuilder<RpcBind, RpcParameters>(this, parameters);
        }

        public void Initialize(EntitySpawnTicket ticket, bool local = true)
        {
            var parameters = new RpcParameters();

            ticket.WriteRPC(this, parameters, local);
        }

        public RpcBind(RpcDelegate Method) : base(Method) { }
    }
    public delegate void RpcDelegate(RpcInfo info);
    public struct RpcParameters : IRpcParameters
    {
        public void WriteTo(INetworkStream stream)
        {

        }
    }

    public class BinaryRpcBind : BaseRpcBind<BinaryRpcDelegate, BinaryRpcParameters>
    {
        public INetworkStream GetSourceStream() => SourceWriterPool.Take();
        public BinarySource GetBinarySource()
        {
            var stream = SourceWriterPool.Take();
            return BinarySource.From(stream);
        }

        static SinglePacketWriter SourceWriterPool = SinglePacketWriter.Create(512);

        internal override void Invoke(ref BinarySource reader, RpcInfo info)
        {
            Method.Invoke(ref reader, info);
        }
        internal override void Invoke(BinaryRpcParameters parameters, RpcInfo info)
        {
            var payload = parameters.Payload;

            var source = BinarySource.From(payload);

            Method(ref source, info);
        }

        public RpcInvocationBuilder<BinaryRpcBind, BinaryRpcParameters> Invoke(Memory<byte> payload)
        {
            var parameters = new BinaryRpcParameters()
            {
                Payload = payload,
            };

            return new RpcInvocationBuilder<BinaryRpcBind, BinaryRpcParameters>(this, parameters);
        }

        public void Initialize(Memory<byte> payload, EntitySpawnTicket ticket, bool local = true)
        {
            var parameters = new BinaryRpcParameters()
            {
                Payload = payload,
            };

            ticket.WriteRPC(this, parameters, local);
        }

        public BinaryRpcBind(BinaryRpcDelegate Method) : base(Method) { }
    }
    public delegate void BinaryRpcDelegate(ref BinarySource binary, RpcInfo info);
    public struct BinaryRpcParameters : IRpcParameters
    {
        public Memory<byte> Payload;

        public void WriteTo(INetworkStream stream)
        {
            var destination = stream.AllocateMemory(Payload.Length);
            Payload.CopyTo(destination);
        }
    }

    public class RpcBind<T1> : BaseRpcBind<RpcDelegate<T1>, RpcParameters<T1>>
    {
        T1 arg1;

        internal override void Invoke(ref BinarySource reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, ref reader);

            Method(arg1, info);
        }
        internal override void Invoke(RpcParameters<T1> parameters, RpcInfo info)
        {
            Method(parameters.Arg1, info);
        }

        public RpcInvocationBuilder<RpcBind<T1>, RpcParameters<T1>> Invoke(T1 arg1)
        {
            var parameters = new RpcParameters<T1>()
            {
                Arg1 = arg1,
            };

            return new RpcInvocationBuilder<RpcBind<T1>, RpcParameters<T1>>(this, parameters);
        }

        public void Initialize(T1 arg1, EntitySpawnTicket ticket, bool local = true)
        {
            var parameters = new RpcParameters<T1>()
            {
                Arg1 = arg1,
            };

            ticket.WriteRPC(this, parameters, local);
        }

        public RpcBind(RpcDelegate<T1> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1>(T1 arg1, RpcInfo info);
    public struct RpcParameters<T1> : IRpcParameters
    {
        public T1 Arg1;

        public void WriteTo(INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in Arg1, stream);
        }
    }

    public class RpcBind<T1, T2> : BaseRpcBind<RpcDelegate<T1, T2>, RpcParameters<T1, T2>>
    {
        T1 arg1;
        T2 arg2;

        internal override void Invoke(ref BinarySource reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, ref reader);
            NetworkSerializer.ReadValue(ref arg2, ref reader);

            Method(arg1, arg2, info);
        }
        internal override void Invoke(RpcParameters<T1, T2> parameters, RpcInfo info)
        {
            Method(parameters.Arg1, parameters.Arg2, info);
        }

        public RpcInvocationBuilder<RpcBind<T1, T2>, RpcParameters<T1, T2>> Invoke(T1 arg1, T2 arg2)
        {
            var parameters = new RpcParameters<T1, T2>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
            };

            return new RpcInvocationBuilder<RpcBind<T1, T2>, RpcParameters<T1, T2>>(this, parameters);
        }

        public void Initialize(T1 arg1, T2 arg2, EntitySpawnTicket ticket, bool local = true)
        {
            var parameters = new RpcParameters<T1, T2>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
            };

            ticket.WriteRPC(this, parameters, local);
        }

        public RpcBind(RpcDelegate<T1, T2> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2>(T1 arg1, T2 arg2, RpcInfo info);
    public struct RpcParameters<T1, T2> : IRpcParameters
    {
        public T1 Arg1;
        public T2 Arg2;

        public void WriteTo(INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in Arg1, stream);
            NetworkSerializer.WriteValue(in Arg2, stream);
        }
    }

    public class RpcBind<T1, T2, T3> : BaseRpcBind<RpcDelegate<T1, T2, T3>, RpcParameters<T1, T2, T3>>
    {
        T1 arg1;
        T2 arg2;
        T3 arg3;

        internal override void Invoke(ref BinarySource reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, ref reader);
            NetworkSerializer.ReadValue(ref arg2, ref reader);
            NetworkSerializer.ReadValue(ref arg3, ref reader);

            Method(arg1, arg2, arg3, info);
        }
        internal override void Invoke(RpcParameters<T1, T2, T3> parameters, RpcInfo info)
        {
            Method(parameters.Arg1, parameters.Arg2, parameters.Arg3, info);
        }

        public RpcInvocationBuilder<RpcBind<T1, T2, T3>, RpcParameters<T1, T2, T3>> Invoke(T1 arg1, T2 arg2, T3 arg3)
        {
            var parameters = new RpcParameters<T1, T2, T3>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
                Arg3 = arg3,
            };

            return new RpcInvocationBuilder<RpcBind<T1, T2, T3>, RpcParameters<T1, T2, T3>>(this, parameters);
        }

        public void Initialize(T1 arg1, T2 arg2, T3 arg3, EntitySpawnTicket ticket, bool local = true)
        {
            var parameters = new RpcParameters<T1, T2, T3>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
                Arg3 = arg3,
            };

            ticket.WriteRPC(this, parameters, local);
        }

        public RpcBind(RpcDelegate<T1, T2, T3> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3, RpcInfo info);
    public struct RpcParameters<T1, T2, T3> : IRpcParameters
    {
        public T1 Arg1;
        public T2 Arg2;
        public T3 Arg3;

        public void WriteTo(INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in Arg1, stream);
            NetworkSerializer.WriteValue(in Arg2, stream);
            NetworkSerializer.WriteValue(in Arg3, stream);
        }
    }

    public class RpcBind<T1, T2, T3, T4> : BaseRpcBind<RpcDelegate<T1, T2, T3, T4>, RpcParameters<T1, T2, T3, T4>>
    {
        T1 arg1;
        T2 arg2;
        T3 arg3;
        T4 arg4;

        internal override void Invoke(ref BinarySource reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, ref reader);
            NetworkSerializer.ReadValue(ref arg2, ref reader);
            NetworkSerializer.ReadValue(ref arg3, ref reader);
            NetworkSerializer.ReadValue(ref arg4, ref reader);

            Method(arg1, arg2, arg3, arg4, info);
        }
        internal override void Invoke(RpcParameters<T1, T2, T3, T4> parameters, RpcInfo info)
        {
            Method(parameters.Arg1, parameters.Arg2, parameters.Arg3, parameters.Arg4, info);
        }

        public RpcInvocationBuilder<RpcBind<T1, T2, T3, T4>, RpcParameters<T1, T2, T3, T4>> Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            var parameters = new RpcParameters<T1, T2, T3, T4>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
                Arg3 = arg3,
                Arg4 = arg4,
            };

            return new RpcInvocationBuilder<RpcBind<T1, T2, T3, T4>, RpcParameters<T1, T2, T3, T4>>(this, parameters);
        }

        public void Initialize(T1 arg1, T2 arg2, T3 arg3, T4 arg4, EntitySpawnTicket ticket, bool local = true)
        {
            var parameters = new RpcParameters<T1, T2, T3, T4>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
                Arg3 = arg3,
                Arg4 = arg4,
            };

            ticket.WriteRPC(this, parameters, local);
        }

        public RpcBind(RpcDelegate<T1, T2, T3, T4> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, RpcInfo info);
    public struct RpcParameters<T1, T2, T3, T4> : IRpcParameters
    {
        public T1 Arg1;
        public T2 Arg2;
        public T3 Arg3;
        public T4 Arg4;

        public void WriteTo(INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in Arg1, stream);
            NetworkSerializer.WriteValue(in Arg2, stream);
            NetworkSerializer.WriteValue(in Arg3, stream);
            NetworkSerializer.WriteValue(in Arg4, stream);
        }
    }

    public class RpcBind<T1, T2, T3, T4, T5> : BaseRpcBind<RpcDelegate<T1, T2, T3, T4, T5>, RpcParameters<T1, T2, T3, T4, T5>>
    {
        T1 arg1;
        T2 arg2;
        T3 arg3;
        T4 arg4;
        T5 arg5;

        internal override void Invoke(ref BinarySource reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, ref reader);
            NetworkSerializer.ReadValue(ref arg2, ref reader);
            NetworkSerializer.ReadValue(ref arg3, ref reader);
            NetworkSerializer.ReadValue(ref arg4, ref reader);
            NetworkSerializer.ReadValue(ref arg5, ref reader);

            Method(arg1, arg2, arg3, arg4, arg5, info);
        }
        internal override void Invoke(RpcParameters<T1, T2, T3, T4, T5> parameters, RpcInfo info)
        {
            Method(parameters.Arg1, parameters.Arg2, parameters.Arg3, parameters.Arg4, parameters.Arg5, info);
        }

        public void Initialize(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, EntitySpawnTicket ticket, bool local = true)
        {
            var parameters = new RpcParameters<T1, T2, T3, T4, T5>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
                Arg3 = arg3,
                Arg4 = arg4,
                Arg5 = arg5,
            };

            ticket.WriteRPC(this, parameters, local);
        }

        public RpcInvocationBuilder<RpcBind<T1, T2, T3, T4, T5>, RpcParameters<T1, T2, T3, T4, T5>> Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            var parameters = new RpcParameters<T1, T2, T3, T4, T5>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
                Arg3 = arg3,
                Arg4 = arg4,
                Arg5 = arg5,
            };

            return new RpcInvocationBuilder<RpcBind<T1, T2, T3, T4, T5>, RpcParameters<T1, T2, T3, T4, T5>>(this, parameters);
        }

        public RpcBind(RpcDelegate<T1, T2, T3, T4, T5> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, RpcInfo info);
    public struct RpcParameters<T1, T2, T3, T4, T5> : IRpcParameters
    {
        public T1 Arg1;
        public T2 Arg2;
        public T3 Arg3;
        public T4 Arg4;
        public T5 Arg5;

        public void WriteTo(INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in Arg1, stream);
            NetworkSerializer.WriteValue(in Arg2, stream);
            NetworkSerializer.WriteValue(in Arg3, stream);
            NetworkSerializer.WriteValue(in Arg4, stream);
            NetworkSerializer.WriteValue(in Arg5, stream);
        }
    }

    public class RpcBind<T1, T2, T3, T4, T5, T6> : BaseRpcBind<RpcDelegate<T1, T2, T3, T4, T5, T6>, RpcParameters<T1, T2, T3, T4, T5, T6>>
    {
        T1 arg1;
        T2 arg2;
        T3 arg3;
        T4 arg4;
        T5 arg5;
        T6 arg6;

        internal override void Invoke(ref BinarySource reader, RpcInfo info)
        {
            NetworkSerializer.ReadValue(ref arg1, ref reader);
            NetworkSerializer.ReadValue(ref arg2, ref reader);
            NetworkSerializer.ReadValue(ref arg3, ref reader);
            NetworkSerializer.ReadValue(ref arg4, ref reader);
            NetworkSerializer.ReadValue(ref arg5, ref reader);
            NetworkSerializer.ReadValue(ref arg6, ref reader);

            Method(arg1, arg2, arg3, arg4, arg5, arg6, info);
        }
        internal override void Invoke(RpcParameters<T1, T2, T3, T4, T5, T6> parameters, RpcInfo info)
        {
            Method(parameters.Arg1, parameters.Arg2, parameters.Arg3, parameters.Arg4, parameters.Arg5, parameters.Arg6, info);
        }

        public RpcInvocationBuilder<RpcBind<T1, T2, T3, T4, T5, T6>, RpcParameters<T1, T2, T3, T4, T5, T6>> Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            var parameters = new RpcParameters<T1, T2, T3, T4, T5, T6>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
                Arg3 = arg3,
                Arg4 = arg4,
                Arg5 = arg5,
                Arg6 = arg6,
            };

            return new RpcInvocationBuilder<RpcBind<T1, T2, T3, T4, T5, T6>, RpcParameters<T1, T2, T3, T4, T5, T6>>(this, parameters);
        }

        public void Initialize(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, EntitySpawnTicket ticket, bool local = true)
        {
            var parameters = new RpcParameters<T1, T2, T3, T4, T5, T6>()
            {
                Arg1 = arg1,
                Arg2 = arg2,
                Arg3 = arg3,
                Arg4 = arg4,
                Arg5 = arg5,
                Arg6 = arg6,
            };

            ticket.WriteRPC(this, parameters, local);
        }

        public RpcBind(RpcDelegate<T1, T2, T3, T4, T5, T6> Method) : base(Method) { }
    }
    public delegate void RpcDelegate<T1, T2, T3, T4, T5, T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, RpcInfo info);
    public struct RpcParameters<T1, T2, T3, T4, T5, T6> : IRpcParameters
    {
        public T1 Arg1;
        public T2 Arg2;
        public T3 Arg3;
        public T4 Arg4;
        public T5 Arg5;
        public T6 Arg6;

        public void WriteTo(INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in Arg1, stream);
            NetworkSerializer.WriteValue(in Arg2, stream);
            NetworkSerializer.WriteValue(in Arg3, stream);
            NetworkSerializer.WriteValue(in Arg4, stream);
            NetworkSerializer.WriteValue(in Arg5, stream);
            NetworkSerializer.WriteValue(in Arg6, stream);
        }
    }

    public struct RpcInfo : ISyncMemberInfo
    {
        NetworkClientID SenderID;

        public DeliveryMethod Delivery { get; }
        public byte Channel { get; }
        public bool IsBuffered { get; }

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

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

        public RpcInfo(NetworkClientID SenderID, byte Channel, DeliveryMethod Delivery, bool IsBuffered)
        {
            this.SenderID = SenderID;
            this.Channel = Channel;
            this.Delivery = Delivery;
            this.IsBuffered = IsBuffered;
        }

        public static RpcInfo FromRemote(ref NetworkRpcCommand command, byte channel, DeliveryMethod delivery)
        {
            return new RpcInfo(command.Sender, channel, delivery, false);
        }
        public static RpcInfo FromLocal<TBind, TParameters>(ref RpcInvocationBuilder<TBind, TParameters> builder)
            where TBind : BaseRpcBind, IBaseRpcBind<TParameters>
            where TParameters : IRpcParameters
        {
            var senderID = Room.Clients.Local.ID;

            return new RpcInfo(senderID, builder.Channel, builder.Delivery, false);
        }
        public static RpcInfo FromBuffer(NetworkClientID senderID) => new RpcInfo(senderID, 0, DeliveryMethod.ReliableOrdered, true);

        public static RpcInfo FromInitialization() => FromInitialization(Room.Clients.Local.ID);
        public static RpcInfo FromInitialization(NetworkClientID senderID) => new RpcInfo(senderID, 0, DeliveryMethod.ReliableOrdered, true);
    }

    public interface IRegisterCustomRPCs
    {
        void RegisterCustomRPCs(List<BaseRpcBind> list);
    }

    public struct RpcInvocationBuilder<TBind, TParameters>
        where TBind : BaseRpcBind, IBaseRpcBind<TParameters>
        where TParameters : IRpcParameters
    {
        internal readonly TBind Bind;
        internal readonly TParameters Parameters;

        internal NetDataWriter PacketWriter;

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

        internal byte Channel;
        public RpcInvocationBuilder<TBind, TParameters> SetChannel(byte value)
        {
            Channel = value;
            return this;
        }
        public RpcInvocationBuilder<TBind, TParameters> SetChannel(NetworkChannelField value)
        {
            Channel = value;
            return this;
        }

        internal DeliveryMethod Delivery;
        public RpcInvocationBuilder<TBind, TParameters> SetDelivery(RemoteSyncDelivery value)
        {
            Delivery = (DeliveryMethod)value;
            return this;
        }

        internal NetworkGroupCollection Groups;
        public RpcInvocationBuilder<TBind, TParameters> SetGroups(NetworkGroupCollection value)
        {
            Groups = value;
            return this;
        }
        public RpcInvocationBuilder<TBind, TParameters> SetGroups(NetworkGroupID value)
        {
            var collection = NetworkGroupCollection.From(value);

            return SetGroups(collection);
        }

        internal RemoteBufferMode BufferMode;
        public RpcInvocationBuilder<TBind, TParameters> SetBufferMode() => SetBufferMode(RemoteBufferMode.Buffer);
        public RpcInvocationBuilder<TBind, TParameters> SetBufferMode(RemoteBufferMode value)
        {
            BufferMode = value;
            return this;
        }

        internal bool IgnoreLocal;
        public RpcInvocationBuilder<TBind, TParameters> SetIgnoreLocal()
        {
            IgnoreLocal = true;
            return this;
        }

        NetworkSyncMemberParameters GetParameters() => new NetworkSyncMemberParameters(Bind.Entity.ID, Bind.Behaviour.ID, Bind.ID);

        void ValidateFinalConfiguration()
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
            ValidateFinalConfiguration();

            //Remote
            {
                var parameters = GetParameters();
                var request = new BroadcastNetworkRpcRequest(BufferMode, Groups, parameters);

                NetworkSerializer.WriteHeader(in request, PacketWriter);

                Parameters.WriteTo(PacketWriter);

                Room.Transport.SendWriter(in PacketWriter, channel: Channel, delivery: Delivery);
            }

            //Local
            InvokeLocal();
        }

        /// <summary>
        /// buffered for all late joining clients, but not broadcasted to currently joining clients
        /// </summary>
        public void Buffer()
        {
            ValidateFinalConfiguration();

            //Remote
            {
                var parameters = GetParameters();
                var request = new BufferNetworkRpcRequest(BufferMode, parameters);

                NetworkSerializer.WriteHeader(in request, PacketWriter);

                Parameters.WriteTo(PacketWriter);

                Room.Transport.SendWriter(in PacketWriter, channel: Channel, delivery: Delivery);
            }
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
            ValidateFinalConfiguration();

            if (BufferMode is not RemoteBufferMode.None)
                NetworkLog.Warning($"Target RPCs Cannot be Buffered, Assigned Buffering Mode will be Ignored");

            //Local if self
            if (Target == Room.Clients.Local.ID)
            {
                InvokeLocal();
                return;
            }

            //Remote if Not
            {
                var parameters = GetParameters();
                var request = new TargetNetworkRpcRequest(Target, parameters);

                NetworkSerializer.WriteHeader(in request, PacketWriter);

                Parameters.WriteTo(PacketWriter);

                Room.Transport.SendWriter(in PacketWriter, channel: Channel, delivery: Delivery);
            }
        }

        void InvokeLocal()
        {
            if (IgnoreLocal)
                return;

            var info = RpcInfo.FromLocal(ref this);
            Bind.Invoke(Parameters, info);
        }

        public RpcInvocationBuilder(TBind Bind, TParameters Parameters)
        {
            this.Bind = Bind;
            this.Parameters = Parameters;

            PacketWriter = Room.Pools.SinglePackerWriter.Take();

            Groups = Bind.Entity.OutputGroups;

            Channel = 0;
            Delivery = DeliveryMethod.ReliableOrdered;
            BufferMode = RemoteBufferMode.None;
            IgnoreLocal = false;
        }
    }
}