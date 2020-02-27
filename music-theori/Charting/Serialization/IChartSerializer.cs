namespace theori.Charting.Serialization
{
    public interface IChartSerializer
    {
        Chart LoadFromFile(ChartInfo chartInfo);
        void SaveToFile(Chart chart);
    }
}
