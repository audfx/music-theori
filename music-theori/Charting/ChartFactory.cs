namespace theori.Charting
{
    public abstract class ChartFactory
    {
        public ChartFactory()
        {
        }

        public virtual Chart CreateNew() => throw new System.NotImplementedException();
    }
}
