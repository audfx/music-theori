using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using theori.Audio;
using theori.IO;
using theori.Platform.Windows;

namespace theori.Core30
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            TestAudio();
        }

        static void TestLoading(string[] args)
        {
            Host.Platform = new WindowsPlatform();

            Host.Platform.LoadLibrary("x64/SDL2.dll");

            Host.DefaultInitialize();
            Host.StartShared(args);
        }

        static void TestAudio()
        {
            Mixer.Initialize(new AudioFormat(48000, 2));

            var track = AudioTrack.FromFile(@"C:\Users\local_i3pxijx\Music\11BringItOn.mp3");
            //var track = AudioTrack.FromFile(@"C:\Users\local_i3pxijx\Music\BringItOnEdited.wav");
            track.Channel = Mixer.MasterChannel;

            track.Play();

            Console.ReadKey();
        }
    }
}
