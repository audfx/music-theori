namespace theori
{
    public static class Time
    {
        public static long HighResolution => System.Diagnostics.Stopwatch.GetTimestamp() * 1_000_000L / System.Diagnostics.Stopwatch.Frequency;

        public static float Delta { get; internal set; }
        public static float FixedDelta { get; internal set; }
        public static float Total { get; internal set; }
    }
}
