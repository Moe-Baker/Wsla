using System;

using Wsla.Serialization;

namespace Wsla
{
    public ref struct EntitySpawnVariableInitializationReader
    {
        INetworkStream Stream;

        public Entry Current { get; private set; }

        public EntitySpawnVariableInitializationReader GetEnumerator() => this;

        public bool MoveNext()
        {
            var behaviour = NetworkSerializer.ReadValue<NetworkBehaviourID>(Stream);
            if (behaviour == NetworkBehaviourID.None)
                return false;

            var variable = NetworkSerializer.ReadValue<NetworkVariableID>(Stream);

            var length = NetworkSerializer.ReadValue<ushort>(Stream);

            var binary = Stream.PopMemory(length);

            Current = new Entry(behaviour, variable, binary);
            return true;
        }

        public EntitySpawnVariableInitializationReader(INetworkStream Stream)
        {
            this.Stream = Stream;
            Current = default;
        }

        public struct Entry
        {
            public NetworkBehaviourID Behaviour { get; }
            public NetworkVariableID Variable { get; }
            public Memory<byte> Binary { get; }

            public Entry(NetworkBehaviourID Behaviour, NetworkVariableID Variable, Memory<byte> Binary)
            {
                this.Behaviour = Behaviour;
                this.Variable = Variable;
                this.Binary = Binary;
            }
        }
    }
}