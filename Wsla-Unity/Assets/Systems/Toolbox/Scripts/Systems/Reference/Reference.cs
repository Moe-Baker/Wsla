using UnityEngine;

namespace Toolbox
{
    public interface IReference<TReference>
    {
        public void Set(TReference value);
    }
}