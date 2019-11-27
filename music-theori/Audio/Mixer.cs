using System;
using System.Collections.Generic;

using NAudio.Wave;

namespace theori.Audio
{
    internal unsafe class MixerChannelToBackendImpl : IWaveProvider
    {
        public bool CanSeek => false;

        public long Position { get => 0; set => throw new NotImplementedException(); }
        public long Length => 0;

        WaveFormat IWaveProvider.WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(Mixer.Format.SampleRate, Mixer.Format.ChannelCount);

        public void Dispose() { }
        public int Read(Span<float> buffer)
        {
            if (Mixer.masterChannel == null) return 0;

            int numRead = Mixer.MasterChannel.Read(buffer);
#if true
            for (int i = 0; i < numRead; i++)
                buffer[i] = MathL.Clamp(buffer[i], -1, 1);
#endif
            return numRead;
        }

        int IWaveProvider.Read(byte[] buffer, int offset, int count)
        {
            fixed (byte *byteBuffer = buffer)
            {
                return Read(new Span<float>(byteBuffer + offset, count / sizeof(float))) * sizeof(float);
            }
        }
    }

    public static class Mixer
    {
        private static readonly object lockObj = new object();

        private static WaveOutEvent? output;
        private static AudioFormat? format;
        internal static MixerChannel? masterChannel;

        public static int OutputLatencyMillis => output?.DesiredLatency ?? throw new InvalidOperationException();
        public static AudioFormat Format => format ?? throw new InvalidOperationException();
        public static MixerChannel MasterChannel => masterChannel ?? throw new InvalidOperationException();

        private static readonly List<MixerChannel> channels = new List<MixerChannel>();

        public static void Initialize(AudioFormat format)
        {
            using var _ = Profiler.Scope("Mixer::Initialize");

            Mixer.format = format;

            output = new WaveOutEvent() { DesiredLatency = 32, NumberOfBuffers = 24 };
            output.Init(new MixerChannelToBackendImpl());

            masterChannel = new MixerChannel("Master", format.ChannelCount, format.SampleRate);
            output.Play();
        }

        public static void Destroy()
        {
            foreach (var c in channels)
                c.Dispose();

            MasterChannel.Dispose();
            output?.Dispose();
        }

        public static void AddChannel(MixerChannel channel)
        {
            lock (lockObj)
            {
                if (!Contains(channel))
                {
                    channels.Add(channel);
                    MasterChannel.AddSource(channel);
                }
            }
        }
        
        public static void RemoveChannel(MixerChannel channel)
        {
            lock (lockObj)
            {
                if (Contains(channel))
                {
                    channels.Remove(channel);
                    MasterChannel.RemoveSource(channel);
                }
            }
        }

        public static bool Contains(MixerChannel channel)
        {
            if (channel == null) return false;
            return channels.Contains(channel);
        }
    }
}
