using LiteNetLib;
using LiteNetLib.Utils;

using System;
using System.Collections.Generic;

using Wsla.Serialization;

namespace Wsla.Server
{
    public class NetworkClient : IDisposable
    {
        public NetworkClientID ID { get; }

        public FixedString20 Username { get; private set; }

        public NetPeer? Peer { get; private set; }
        internal void AssignPeer(NetPeer value)
        {
            this.Peer = value;
        }

        public NetworkClientVersion Version { get; private set; }

        public bool IsMaster => Room.Clients.Master == this;

        #region Spawn Tokens
        public Queue<NetworkEntityID> SpawnTokens { get; }
        public byte SpawnAllowance => (byte)SpawnTokens.Count;

        public void AddSpawnToken(NetworkEntityID id)
        {
            SpawnTokens.Enqueue(id);
        }
        public NetworkEntityID RemoveSpawnToken()
        {
            return SpawnTokens.Dequeue();
        }

        public bool ValdiateSpawnToken(NetworkEntityID target)
        {
            if (SpawnTokens.TryPeek(out var registerd) is false)
            {
                NetworkLog.Warning($"No Spawn Tokens Available");
                return false;
            }

            if (registerd != target)
            {
                NetworkLog.Warning($"Expected Token {registerd} Got {target}");
                return false;
            }

            RemoveSpawnToken();
            return true;
        }

        public void WriteSpawnTokens(NetDataWriter writer)
        {
            foreach (var token in SpawnTokens)
                NetworkSerializer.WriteValue(token, writer);
        }
        #endregion

        #region Entities
        public ExpandList<NetworkEntity> Entities { get; }

        public void RegisterEntity(NetworkEntity target)
        {
            target.OwnerRegisteration = Entities.Add(target);
        }
        public void UnregisterEntity(NetworkEntity target)
        {
            Entities.RemoveAt(target.OwnerRegisteration);
        }
        #endregion

        public void WriteState(NetDataWriter writer)
        {
            NetworkSerializer.WriteValue(ID, writer);
            NetworkSerializer.WriteValue(Username, writer);
        }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public void Dispose()
        {

        }

        readonly Room Room;
        public NetworkClient(Room Room, NetworkClientID ID, FixedString20 Username, int SpawnTokenCapacity, NetworkClientVersion Version)
        {
            this.Room = Room;

            this.ID = ID;
            this.Username = Username;
            this.Version = Version;

            SpawnTokens = new Queue<NetworkEntityID>(SpawnTokenCapacity);

            Entities = new(0);
        }
    }

    [Serializable]
    [NetworkBlittable]
    public partial struct NetworkClientVersion : IEquatable<NetworkClientVersion>
    {
        public uint Value { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj is NetworkClientVersion other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkClientVersion other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => (int)Value;

        public override string ToString() => Value.ToString();

        public NetworkClientVersion(uint value)
        {
            this.Value = value;
        }

        public static NetworkClientVersion Min { get; } = new(uint.MinValue);
        public static NetworkClientVersion Max { get; } = new(uint.MaxValue);

        public static bool operator ==(NetworkClientVersion left, NetworkClientVersion right) => left.Equals(right);
        public static bool operator !=(NetworkClientVersion left, NetworkClientVersion right) => !left.Equals(right);

        public static NetworkClientVersion Increment(NetworkClientVersion index)
        {
            unchecked
            {
                return new NetworkClientVersion(index.Value + 1);
            }
        }
    }
}