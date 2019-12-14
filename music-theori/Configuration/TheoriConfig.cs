using theori.Graphics;

namespace theori.Configuration
{
    public class TheoriConfig : Config<TheoriConfigKey>
    {
        public static string ChartsDirectory { get; set; } = "charts";

        protected override void SetDefaults()
        {
	        Set(TheoriConfigKey.ScreenWidth, 1280);
	        Set(TheoriConfigKey.ScreenHeight, 720);
	        Set(TheoriConfigKey.FullScreenWidth, -1);
	        Set(TheoriConfigKey.FullScreenHeight, -1);
	        Set(TheoriConfigKey.Fullscreen, false);
            Set(TheoriConfigKey.FullscreenMonitorIndex, 0);
            Set(TheoriConfigKey.Maximized, false);
            Set(TheoriConfigKey.MasterVolume, 0.65f);
	        Set(TheoriConfigKey.ScreenX, -1);
	        Set(TheoriConfigKey.ScreenY, -1);
	        Set(TheoriConfigKey.VSync, VSyncMode.Off);
	        Set(TheoriConfigKey.FPSTarget, 0);
            Set(TheoriConfigKey.Controller_DeviceID, 0);
        }
    }

    public enum TheoriConfigKey
    {
	    ScreenWidth,
	    ScreenHeight,
	    FullScreenWidth,
	    FullScreenHeight,
	    ScreenX,
	    ScreenY,
	    Fullscreen,
	    FullscreenMonitorIndex,
	    VSync,
        Maximized,

        MasterVolume,
        FPSTarget,

        Controller_DeviceID,
    }
}
