using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using theori.IO;

namespace theori.Platform.Windows
{
    internal static class Win32
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);
    }

    public sealed class WindowsPlatform : IPlatform
    {
        public IntPtr LoadLibrary(string libraryName) => Win32.LoadLibrary(libraryName);
        public void FreeLibrary(IntPtr library) => Win32.FreeLibrary(library);
        public IntPtr GetProcAddress(IntPtr library, string procName) => Win32.GetProcAddress(library, procName);
    }
}
