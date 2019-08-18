using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using theori.Audio;
using theori.IO;

namespace theori.Core30
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Host.Platform = new CorePlatform();

            Host.Platform.LoadLibrary("x64/SDL2.dll");

            Host.DefaultInitialize();
            Host.StartShared(args);
        }

        static void TestAudio()
        {
            Mixer.Initialize(new AudioFormat(48000, 2));

            var track = AudioTrack.FromFile(@"B:\kshootmania\songs\Sound Voltex\allclear_skydelta\track.ogg");
            track.Channel = Mixer.MasterChannel;

            track.Play();

            Console.ReadKey();
        }
    }

    internal static class Win32
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);
    }

    public sealed class CorePlatform : Platform.IPlatform
    {
        public IntPtr LoadLibrary(string libraryName) => Win32.LoadLibrary(libraryName);
        public void FreeLibrary(IntPtr library) => Win32.FreeLibrary(library);
        public IntPtr GetProcAddress(IntPtr library, string procName) => Win32.GetProcAddress(library, procName);

        public OpenFileResult ShowOpenFileDialog(OpenFileDialogDesc desc)
        {
            throw new NotImplementedException();
        }

        public FolderBrowserResult ShowFolderBrowserDialog(FolderBrowserDialogDesc desc)
        {
            throw new NotImplementedException();
        }
    }
}
