namespace theori.Audio
{
    // TODO(local): `Ogg` and `Wave` is not very descriptive.
    // TODO(local): `Ogg` and `Wave` is not very descriptive.
    // TODO(local): `Ogg` and `Wave` is not very descriptive.

    public enum AudioEncoding
    {
        Unknown,

        Ogg,
        Wave,
    }

    /// <summary>
    /// All audio formats are assumed to use 32 bit float samples.
    /// </summary>
    public class AudioFormat
    {
        public int SampleRate { get; }
        public int ChannelCount { get; }

        public AudioFormat(int sampleRate, int channelCount)
        {
            SampleRate = sampleRate;
            ChannelCount = channelCount;
        }
    }
}
