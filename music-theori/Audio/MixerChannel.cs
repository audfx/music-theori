using System;
using System.Collections.Generic;

using NAudio;
using NAudio.Wave;

namespace theori.Audio
{
    public class MixerChannel : AudioSource
    {
        private readonly object lockObj = new object();
        private readonly List<AudioSource> sources = new List<AudioSource>();

        private float[] mixerBuffer = new float[1024];

        public string Name { get; }

        public override int Channels { get; }
        public override int SampleRate { get; }

        public override bool CanSeek => false;

        public override time_t Length => 0;
        public override time_t Position { get => 0; set => Seek(value); }

        public event Action<AudioSource> OnSampleSourceEnded;

        public MixerChannel(string name, int channelCount, int sampleRate)
        {
            Name = name;
            Channels = channelCount;
            SampleRate = sampleRate;
        }

        internal void AddSource(AudioSource source)
        {
            lock (lockObj)
            {
                if (!Contains(source)) sources.Add(source);
            }
        }
        
        internal void RemoveSource(AudioSource source)
        {
            lock (lockObj)
            {
                if (Contains(source)) sources.Remove(source);
            }
        }

        internal bool Contains(AudioSource source)
        {
            if (source == null) return false;
            return sources.Contains(source);
        }

        public override int Read(Span<float> buffer)
        {
            int numStoredSamples = 0;
            int count = buffer.Length;

            if (count > 0 && sources.Count > 0)
            {
                lock (lockObj)
                {
                    mixerBuffer = mixerBuffer.CheckBuffer(count);
                    var numReadSamples = new List<int>();

                    for (int m = sources.Count - 1; m >=0; m--)
                    {
                        if (m >= sources.Count) continue;
                        var source = sources[m];

                        int read = source.Read(mixerBuffer.AsSpan(0, count));
                        for (int i = 0; i < read; i++)
                        {
                            if (numStoredSamples <= i)
                                buffer[i] = mixerBuffer[i];
                            else buffer[i] += mixerBuffer[i];
                        }

                        if (read > numStoredSamples)
                            numStoredSamples = read;

                        if (read > 0)
                            numReadSamples.Add(read);
                        else
                        {
                            source.OnFinish();
                            OnSampleSourceEnded?.Invoke(source);
                            if (source.RemoveFromChannelOnFinish)
                                RemoveSource(source);
                        }
                    }
                }
            }

            float vol = Volume;

            for (int i = 0; i < numStoredSamples; i++)
                buffer[i] *= vol;
            for (int i = numStoredSamples; i < count; i++)
                buffer[i] = 0;

            return count;
        }

        public override void Seek(time_t positionMicros) => throw new NotImplementedException("cannot seek");

        protected override void DisposeManaged()
        {
            lock (lockObj)
            {
                foreach (var sampleSource in sources.ToArray())
                {
                    sampleSource.Dispose();
                    sources.Remove(sampleSource);
                }
            }
        }
    }
}
