namespace Toolbox
{
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