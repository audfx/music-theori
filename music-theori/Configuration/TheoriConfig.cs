using theori.Graphics;

namespace theori.Configuration
{
    [ConfigGroup("theori")]
    public static class TheoriConfig
    {
        [Config] public static int WindowWidth { get; set; } = 1280;
        [Config] public static int WindowHeight { get; set; } = 720;
        [Config] public static int FullscreenWidth { get; set; } = -1;
        [Config] public static int FullscreenHeight { get; set; } = -1;
        [Config] public static int ScreenX { get; set; } = -1;
        [Config] public static int ScreenY { get; set; } = -1;
        [Config] public static VSyncMode VerticalSync { get; set; } = VSyncMode.Off;
        [Config] public static bool Fullscreen { get; set; } = false;
        [Config] public static bool Maximized { get; set; } = false;
        [Config] public static int FullscreenMonitorIndex { get; set; } = 0;
        [Config] public static int FpsTarget { get; set; } = 0;
        [Config] public static float MasterVolume { get; set; } = 0.6f;
        [Config] public static string ChartsDirectory { get; set; } = "charts";
        [Config] public static string? SelectedController { get; set; } = null;
    }
}
