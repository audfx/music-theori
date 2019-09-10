using System;
using System.IO;

using NVorbis;

namespace theori.Audio.NVorbis
{
    internal sealed class NVorbisSource : ISampleSource
    {
        // This should be disposed thru the vorbis reader
#pragma warning disable IDE0069 // Disposable fields should be disposed
        private Stream? m_stream;
#pragma warning restore IDE0069 // Disposable fields should be disposed
        private VorbisReader? m_vorbisReader;

        private float[] m_buffer = new float[1024];

        public NVorbisSource(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if(!stream.CanRead)
                throw new ArgumentException("Stream is not readable.", "stream");

            m_stream = stream;
            m_vorbisReader = new VorbisReader(stream, true);

            Format = new AudioFormat(m_vorbisReader.SampleRate, m_vorbisReader.Channels);
        }

        public bool CanSeek => m_stream?.CanSeek ?? throw new InvalidOperationException();
        public AudioFormat Format { get; private set; }

        public time_t Length => CanSeek ? m_vorbisReader!.TotalTime.TotalSeconds : 0;

        public time_t Position
        {
            get => CanSeek ? m_vorbisReader!.DecodedTime.TotalSeconds : 0;
            set
            {
                if(!CanSeek)
                    throw new InvalidOperationException("NVorbisSource is not seekable.");
                if (value < 0 || value > Length) 
                    throw new ArgumentOutOfRangeException("value");

                m_vorbisReader!.DecodedTime = TimeSpan.FromSeconds((double)value);
            }
        }

        public int Read(Span<float> buffer)
        {
            if (m_vorbisReader == null)
                return 0;

            m_buffer = m_buffer.CheckBuffer(buffer.Length);
            int result = m_vorbisReader.ReadSamples(m_buffer, 0, buffer.Length);
            m_buffer.AsSpan(0, result).CopyTo(buffer);
            return result;
        }

        public void Dispose()
        {
            m_vorbisReader?.Dispose();
            m_vorbisReader = null;

            m_stream = null;
        }
    }
}
