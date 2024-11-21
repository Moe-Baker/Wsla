using Wsla.Serialization;

namespace Wsla.Unity
{
    public interface ITraitHandler
    {
        void ReadTrait(INetworkStream stream);
    }

    public interface ITraitHandler<T> : ITraitHandler
    {
        void ITraitHandler.ReadTrait(INetworkStream stream)
        {
            var value = NetworkSerializer.ReadValue<T>(stream);
            ApplyTrait(value);
        }

        void ApplyTrait(T value);
    }
}