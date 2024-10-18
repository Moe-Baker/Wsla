using UnityEngine;

namespace Wsla.Unity
{
    public interface INetworkBehaviour
    {
        void Set(NetworkEntity.Behaviour reference);
    }

    public abstract class NetworkBehaviour : MonoBehaviour, INetworkBehaviour
    {
        public NetworkEntity.Behaviour Network { get; private set; }

        public virtual void Set(NetworkEntity.Behaviour reference)
        {
            Network = reference;
        }
    }
}