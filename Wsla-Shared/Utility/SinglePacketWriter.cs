using LiteNetLib.Utils;

namespace Wsla
{
    public struct SinglePacketWriter
    {
        NetDataWriter Instance;

        public NetDataWriter Take()
        {
            Instance.SetPosition(0);
            return Instance;
        }

        public SinglePacketWriter(NetDataWriter Instance)
        {
            this.Instance = Instance;
        }

        public static SinglePacketWriter Create(int capacity)
        {
            var instance = new NetDataWriter(true, capacity);
            return new SinglePacketWriter(instance);
        }
    }
}