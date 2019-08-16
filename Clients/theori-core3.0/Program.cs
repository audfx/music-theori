using System;

using theori.Audio;

namespace theori.Core30
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Mixer.Initialize(new AudioFormat(48000, 2));

            var track = AudioTrack.FromFile(@"B:\kshootmania\songs\Sound Voltex\allclear_skydelta\track.ogg");
            track.Channel = Mixer.MasterChannel;

            track.Play();

            Console.ReadKey();
        }
    }
}
