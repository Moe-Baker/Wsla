namespace Toolbox
{
    public static class ChangePairData
    {
        public static ChangePairData<T> Create<T>(T previous, T current) => new(previous, current);

        public static ChangePairData<T> FromPrevious<T>(T previous) => new(previous, default);
        public static ChangePairData<T> FromCurrent<T>(T current) => new(default, current);
    }

    public struct ChangePairData<T>
    {
        public T Previous { get; private set; }
        public T Current { get; private set; }

        public ChangePairData(T Previous, T Current)
        {
            this.Previous = Previous;
            this.Current = Current;
        }
    }
}