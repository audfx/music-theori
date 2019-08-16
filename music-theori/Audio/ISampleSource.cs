using System;

using NAudio.Wave;

namespace theori.Audio
{
    public interface ISampleSource : IDisposable
    {
        bool CanSeek { get; }
        AudioFormat Format { get; }

        time_t Length { get; }
        time_t Position { get; set; }

        int Read(Span<float> buffer);
    }
}
