using System.Collections.Generic;

using theori.Charting;
using theori.Configuration;

namespace theori.Database
{
    public static class ChartDatabaseService
    {
        private static ChartDatabase? m_database;

        public static IEnumerable<ChartSetInfo> ChartSets => m_database!.ChartSets;
        public static IEnumerable<ChartInfo> Charts => m_database!.Charts;

        public static string[] CollectionNames => m_database!.GetCollectionNames();
        public static void CreateCollection(string collectionName) => m_database!.CreateCollection(collectionName);
        public static void AddToCollection(string collectionName, ChartInfo chart) => m_database!.AddChartToCollection(collectionName, chart);
        public static void RemoveFromCollection(string collectionName, ChartInfo chart) => m_database!.RemoveChartFromCollection(collectionName, chart);
        public static IEnumerable<ChartInfo> GetChartsInCollection(string collectionName) => m_database!.GetChartsInCollection(collectionName);

        public static string ChartsDirectory => TheoriConfig.ChartsDirectory;

        public static void Initialize(string localDatabaseName = "theori-charts.sqlite")
        {
            using var _ = Profiler.Scope("ChartDatabaseService::Initialize");

            m_database = new ChartDatabase(localDatabaseName);
            m_database.OpenLocal();
        }

        public static void Destroy()
        {
            m_database!.SaveData();
            m_database!.Close();
            m_database = null;
        }

        public static bool ContainsSetAtLocation(string setLocation) => m_database!.ContainsSetAtLocation(setLocation);

        public static void AddSet(ChartSetInfo setInfo) => m_database!.AddSet(setInfo);

        public static void RemoveSet(ChartSetInfo setInfo) => m_database!.RemoveSet(setInfo);
        public static void RemoveChart(ChartInfo chartInfo) => m_database!.RemoveChart(chartInfo);

        public static string GetLocalConfigForChart(ChartInfo chartInfo) => m_database!.GetLocalConfigForChart(chartInfo);
        public static void SaveLocalConfigForChart(ChartInfo chartInfo, string config) => m_database!.SaveLocalConfigForChart(chartInfo, config);
    }
}
