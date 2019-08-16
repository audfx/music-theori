using System;
using System.IO;
using theori.Audio;

namespace theori.Net472
{
    public static class Program
    {
        static void Main(string[] args)
        {
            AudioFactory.TempAudioSource = AudioFactory.CreateAudioSource(File.OpenRead(@"B:\kshootmania\songs\Sound Voltex\allclear_skydelta\track.ogg"));
            AudioFactory.OutputDevice.Begin();

            Console.ReadKey();
        }
    }
}
