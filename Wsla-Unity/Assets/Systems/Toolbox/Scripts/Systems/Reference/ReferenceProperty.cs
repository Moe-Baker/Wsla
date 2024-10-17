using System;

namespace Toolbox
{
    [Serializable]
    public class ReferenceProperty<TReference> : IReference<TReference>
    {
        [field: NonSerialized]
        public TReference Reference { get; private set; }

        public virtual void Set(TReference value)
        {
            this.Reference = value;
        }
    }
}