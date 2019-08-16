using System;
using System.IO;

using NVorbis;

namespace theori.Audio.NAudio
{
    internal sealed class NAudioOggAudioSource : IAudioSource, IDisposable
    {
        private VorbisReader m_reader = null;

        public NAudioOggAudioSource(Stream oggStream)
        {
            if (oggStream == null)
                throw new ArgumentNullException(nameof(oggStream));
            if (!oggStream.CanRead)
                throw new ArgumentException("Stream is not readable.", nameof(oggStream));

            m_reader = new VorbisReader(oggStream, true);
        }

        void IDisposable.Dispose()
        {
            m_reader?.Dispose();
            m_reader = null;
        }

        int IAudioSource.Read(float[] destBuffer, int offset, int count)
        {
            return m_reader.ReadSamples(destBuffer, offset, count);
        }
    }
}
