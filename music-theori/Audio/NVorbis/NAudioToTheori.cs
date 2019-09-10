using System;
using System.Collections.Generic;
using System.Text;

using NAudio;
using NAudio.Wave;

namespace theori.Audio.NVorbis
{
    internal unsafe sealed class NAudioToTheori : ISampleSource
    {
        public bool CanSeek { get; }

        public AudioFormat Format { get; }

        public time_t Length { get; }
        public time_t Position { get => m_stream.CurrentTime.TotalSeconds; set => m_stream.CurrentTime = TimeSpan.FromSeconds((double)value); }

        private readonly WaveStream m_stream;
        private byte[] m_buffer = new byte[1024];

        public NAudioToTheori(WaveStream stream)
        {
            stream = WaveFormatConversionStream.CreatePcmStream(stream);
            m_stream = new WaveChannel32(stream);

            CanSeek = stream.CanSeek;
            Format = new AudioFormat(stream.WaveFormat.SampleRate, stream.WaveFormat.Channels);
            Length = stream.TotalTime.TotalSeconds;
        }

        public void Dispose()
        {
            m_stream.Dispose();
        }

        public int Read(Span<float> buffer)
        {
            int byteCount = buffer.Length * sizeof(float);
            m_buffer = m_buffer.CheckBuffer(byteCount);

            int result = m_stream.Read(m_buffer, 0, byteCount);
            fixed (byte *bufferPointer = m_buffer)
            {
                new Span<float>((float *)bufferPointer, buffer.Length).CopyTo(buffer);
            }

            return result;
        }
    }
}
