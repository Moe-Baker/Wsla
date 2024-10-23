using System;

using LiteNetLib;

using UnityEngine;

using Wsla.Serialization;

namespace Wsla.Unity
{
    public sealed class NetworkEntity : MonoBehaviour
    {
        public NetworkEntityID ID { get; private set; }
        public NetworkEntitySource Source { get; private set; }
        public NetworkEntityResource Resource { get; private set; }

        internal void SetProperties(NetworkEntityID ID, NetworkEntitySource Source, NetworkEntityResource Resource)
        {
            this.ID = ID;
            this.Source = Source;
            this.Resource = Resource;
        }

        public bool IsSpawned { get; private set; }
        internal void Spawn()
        {
            IsSpawned = true;

            OnSpawn?.Invoke();
        }
        public event Action OnSpawn;

        public bool IsReplicated { get; private set; }
        internal void Replicate()
        {
            IsReplicated = true;

            OnReplicated?.Invoke();
        }
        public event Action OnReplicated;

        public void ReadState(ref NetPacketReader reader)
        {
            ID = NetworkSerializer.ReadValue<NetworkEntityID, NetPacketReader>(ref reader);
            Source = NetworkSerializer.ReadValue<NetworkEntitySource, NetPacketReader>(ref reader);
            Resource = NetworkSerializer.ReadValue<NetworkEntityResource, NetPacketReader>(ref reader);
        }

        public class Behaviour
        {

        }

        //Static Utility
        public static void ReadProperties(NetPacketReader reader, out NetworkEntitySource source, out NetworkEntityResource resource, out NetworkEntityID id)
        {
            source = NetworkSerializer.ReadValue<NetworkEntitySource, NetPacketReader>(ref reader);
            resource = NetworkSerializer.ReadValue<NetworkEntityResource, NetPacketReader>(ref reader);
            id = NetworkSerializer.ReadValue<NetworkEntityID, NetPacketReader>(ref reader);
        }
    }
}