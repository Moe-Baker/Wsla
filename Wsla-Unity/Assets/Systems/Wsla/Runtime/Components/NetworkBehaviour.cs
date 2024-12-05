using System;

using UnityEngine;

using Wsla.Serialization;

namespace Wsla.Unity
{
    public interface INetworkBehaviour
    {
        public NetworkEntity.Behaviour Network { get; }

        void Set(NetworkEntity.Behaviour reference);
    }

    public abstract partial class NetworkBehaviour : MonoBehaviour, INetworkBehaviour
    {
        public NetworkEntity.Behaviour Network { get; private set; }

        public virtual void Set(NetworkEntity.Behaviour reference)
        {
            Network = reference;
        }
    }
}