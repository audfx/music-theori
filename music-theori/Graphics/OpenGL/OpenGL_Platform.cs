using System;
using System.Runtime.InteropServices;

namespace theori.Graphics.OpenGL
{
    internal static class GlPlatform
    {
        static readonly IGlPlatformLayer platform;

        public const string OpenGL32 = "opengl32.dll";

        static GlPlatform()
        {
            platform = RuntimeInfo.IsWindows ? (IGlPlatformLayer)new WindowsGl() : (IGlPlatformLayer)new LinuxGl();
            IntPtr glLibrary = platform.LoadDynLib(OpenGL32);
        }

        public static IntPtr LoadDynLib(string name) => platform.LoadDynLib(name);
        public static IntPtr GlGetProcAddress(string name) => platform.GlGetProcAddress(name);
    }

    internal interface IGlPlatformLayer
    {
        IntPtr LoadDynLib(string name);
        IntPtr GlGetProcAddress(string name);
    }

    internal class LinuxGl : IGlPlatformLayer
    {
        private const int RTLD_NOW = 0x00002;

        [DllImport("libdl.so", SetLastError = true)]
        private static extern IntPtr dlopen(string filename, int flag);

        [DllImport("libEGL.so", SetLastError = true)]
        private static extern IntPtr eglGetProcAddress(string name);

        public IntPtr LoadDynLib(string name) => dlopen(name, RTLD_NOW);
        public IntPtr GlGetProcAddress(string name) => eglGetProcAddress(name);
    }

	internal class WindowsGl : IGlPlatformLayer
    {
        private const string Kernel32 = "kernel32.dll";
        private const string OpenGL32 = "opengl32.dll";

        [DllImport(Kernel32, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport(OpenGL32, SetLastError = true)]
        private static extern IntPtr wglGetProcAddress(string name);

        public IntPtr LoadDynLib(string name) => LoadLibrary(name);
        public IntPtr GlGetProcAddress(string name) => wglGetProcAddress(name);
    }
}
