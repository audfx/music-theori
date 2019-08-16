using System;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace theori.Audio
{
    public sealed class ResamplingSampleSource : Disposable, ISampleSource
    {
        class SourceToProvider : ISampleProvider
        {
            public readonly ISampleSource m_source;
            public WaveFormat WaveFormat { get; }

            public SourceToProvider(ISampleSource source)
            {
                m_source = source;

                var audioFormat = source.Format;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(audioFormat.SampleRate, audioFormat.ChannelCount);
            }

            public int Read(float[] buffer, int offset, int count) =>
                m_source.Read(new Span<float>(buffer, offset, count));
        }

        bool ISampleSource.CanSeek => m_source.CanSeek;
        public AudioFormat Format { get; }

        public time_t Length => m_source.Length;
        public time_t Position { get => m_source.Position; set => m_source.Position = value; }

        private readonly ISampleSource m_source;
        private readonly WdlResamplingSampleProvider m_provider;

        private float[] m_buffer = new float[1024];

        public ResamplingSampleSource(ISampleSource source, AudioFormat desiredFormat)
        {
            m_source = source;
            Format = desiredFormat;

            m_provider = new WdlResamplingSampleProvider(new SourceToProvider(source), desiredFormat.SampleRate);
        }

        protected override void DisposeManaged()
        {
            m_source.Dispose();
        }

        public int Read(Span<float> buffer)
        {
            m_buffer = m_buffer.CheckBuffer(buffer.Length);
            int result = m_provider.Read(m_buffer, 0, buffer.Length);
            m_buffer.AsSpan(0, result).CopyTo(buffer);
            return result;
        }
    }
}
