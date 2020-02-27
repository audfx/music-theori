namespace theori.Charting.Playback
{
    public delegate void WindowEntityEvent(HybridLabel laneLabel, Entity entity);

    public interface IChartWindow
    {
        Chart Chart { get; }

        time_t Position { get; set; }
        time_t LookBehind { get; set; }
        time_t LookAhead { get; set; }

        event WindowEntityEvent? EntityEnter;
        event WindowEntityEvent? EntityExit;
    }
}
