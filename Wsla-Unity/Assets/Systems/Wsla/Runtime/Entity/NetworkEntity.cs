using System;

using LiteNetLib;

using UnityEngine;

using Wsla.Shared.Global;

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

        public void ReadState(in NetPacketReader reader)
        {
            ID = NetworkSerializer.ReadValue<NetworkEntityID>(in reader);
            Source = NetworkSerializer.ReadValue<NetworkEntitySource>(in reader);
            Resource = NetworkSerializer.ReadValue<NetworkEntityResource>(in reader);
        }

        public class Behaviour
        {

        }

        //Static Utility
        public static void ReadProperties(NetPacketReader reader, out NetworkEntitySource source, out NetworkEntityResource resource, out NetworkEntityID id)
        {
            source = NetworkSerializer.ReadValue<NetworkEntitySource>(in reader);
            resource = NetworkSerializer.ReadValue<NetworkEntityResource>(in reader);
            id = NetworkSerializer.ReadValue<NetworkEntityID>(in reader);
        }
    }
}