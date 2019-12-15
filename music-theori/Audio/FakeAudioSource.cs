using System;

using MoonSharp.Interpreter;

namespace theori.Audio
{
    public class FakeAudioSource : AudioTrack
    {
        private readonly int m_sampleRate, m_channels;
        private long m_samplePos = 0;

        public override bool CanSeek => true;
        public override int SampleRate => m_sampleRate;
        public override int Channels => m_channels;

        public override time_t Position
        { 
            get => (double)m_samplePos / (m_sampleRate * m_channels);
            set => m_samplePos = (long)((double)value * (m_sampleRate * m_channels));
        }

        public override time_t Length => 0;

        public FakeAudioSource(int sampleRate = 44100, int channels = 2)
        {
            m_sampleRate = sampleRate;
            m_channels = channels;
        }

        private long TimeToSamples(time_t time) => (long)(time.Seconds * m_samplePos * m_channels);

        private (long Repeat, long Check)? m_loopArea = null;
        private bool m_isLoopingArea = false;

        public override void SetLoopAreaSamples(long start, long end) => m_loopArea = (start, end);
        public override void SetLoopArea(time_t start, time_t end) => m_loopArea = (TimeToSamples(start), TimeToSamples(end));
        public override void RemoveLoopArea() => m_loopArea = null;

        [MoonSharpHidden]
        public override int Read(Span<float> buffer)
        {
            //if (IsPlaying) m_samplePos += buffer.Length;
            
            int count = buffer.Length;
            switch (PlaybackState)
            {
                case PlaybackState.Playing:
                {
                    // TODO(local): playback speed and looping
                    m_samplePos += count;
                    buffer.Fill(0);
                } break;

                case PlaybackState.Stopped: buffer.Fill(0); break;

                default: return 0;
            }

            return buffer.Length;
        }

        [MoonSharpHidden]
        public override void Seek(time_t position)
        {
            m_samplePos = (long)((double)position * (m_sampleRate * m_channels));
        }
    }
}
