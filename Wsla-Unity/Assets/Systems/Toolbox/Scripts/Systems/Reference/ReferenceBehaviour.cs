using UnityEngine;

namespace Toolbox
{
    public class ReferenceBehaviour<TReference> : MonoBehaviour, IReference<TReference>
    {
        public TReference Reference { get; protected set; }

        public virtual void Set(TReference value)
        {
            Reference = value;
        }
    }
}