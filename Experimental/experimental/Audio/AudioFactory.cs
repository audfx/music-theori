using NAudio.Wave;

using theori.Audio.NAudio;

namespace theori.Audio
{
    public static class AudioFactory
    {
        public static IAudioOutputDevice OutputDevice = new NAudioOutputDevice(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));

        public static IAudioSource TempAudioSource;

        public static IAudioSource CreateAudioSource(System.IO.Stream inputStream)
        {
            return new NAudioOggAudioSource(inputStream);
        }
    }
}
