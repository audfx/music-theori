using System;
using System.IO;

using NAudio.Wave;

using theori.Audio.NVorbis;

namespace theori.Audio
{
    public enum PlaybackState
    {
        Stopped = 0,
        Playing,
    }

    public sealed class AudioTrack : AudioSource
    {
        internal static AudioTrack CreateUninitialized() => new AudioTrack();

        public static AudioTrack FromFile(string fileName)
        {
            return FromStream(Path.GetExtension(fileName), File.OpenRead(fileName));
        }

        public static AudioTrack FromStream(string ext, Stream stream)
        {
            var source = ext switch
            {
                ".mp3" => (ISampleSource)new NAudioToTheori(new Mp3FileReader(stream)),
                ".wav" => (ISampleSource)new NAudioToTheori(new WaveFileReader(stream)),
                ".ogg" => (ISampleSource)new NVorbisSource(stream),
                _ => throw new NotImplementedException(),
            };
            var sampleSource = new ResamplingSampleSource(source, Mixer.Format);
            return new AudioTrack(sampleSource);
        }

        internal ISampleSource? Source { get; private set; }

        public override bool CanSeek => Source?.CanSeek ?? throw new InvalidOperationException();

        public override int SampleRate => Source?.Format.SampleRate ?? throw new InvalidOperationException();
        public override int Channels => Source?.Format.ChannelCount ?? throw new InvalidOperationException();

        internal AudioFormat Format => Source?.Format ?? throw new InvalidOperationException();

        private time_t m_lastSourcePosition;

        private time_t m_positionCached;
        public override time_t Position
        {
            get
            {
                if (PlaybackState != PlaybackState.Playing && m_positionCached >= 0)
                    return m_positionCached;
                return m_lastSourcePosition;
            }

            set
            {
                if (PlaybackState != PlaybackState.Playing)
                    m_positionCached = value;
                Seek(value);
            }
        }

        public override time_t Length => Source?.Length ?? throw new InvalidOperationException();

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        private float m_playbackSpeed = 1, m_invPlaybackSpeed = 1;
        public float PlaybackSpeed
        {
            get => m_playbackSpeed;
            set => m_invPlaybackSpeed = 1 / (m_playbackSpeed = MathL.Clamp(value, 0.1f, 9999));
        }

        internal AudioTrack()
        {
        }

        internal AudioTrack(ISampleSource source)
        {
            Source = source;
        }

        /// <summary>
        /// If this track already has a source, it will dispose of the input source for
        ///  you since it will not take ownership.
        /// I guess that IS still taking ownership, you just don't need to worry about it.
        /// </summary>
        internal void SetSourceFromStream(Stream stream, string ext)
        {
            if (Source != null)
            {
                // kill it sorry
                stream.Dispose();
                return;
            }
            ISampleSource source = ext switch
            {
                //".wav" => new WaveFileReader(stream),
                ".ogg" => new NVorbisSource(stream),
                _ => throw new NotImplementedException(),
            };
            Source = new ResamplingSampleSource(source, Mixer.Format);
        }

        public void Play()
        {
            if (PlaybackState == PlaybackState.Playing)
                return;

            m_positionCached = -1;
            PlaybackState = PlaybackState.Playing;
        }

        public void Replay()
        {
            Stop();
            Seek(0);
            Play();
        }

        public void Stop()
        {
            if (PlaybackState == PlaybackState.Stopped)
                return;

            PlaybackState = PlaybackState.Stopped;
        }

        private long m_realSampleIndex;
        private float[] m_resampleBuffer = new float[2048];

        private (long Repeat, long Check)? m_loopArea = null;
        private bool m_isLoopingArea = false;

        private long TimeToSamples(time_t time) => (long)(time.Seconds * Source!.Format.SampleRate * Source!.Format.ChannelCount);

        public void SetLoopAreaSamples(long start, long end)
        {
            if (!CanSeek) throw new InvalidOperationException("can't set loop area: cannot seek");
            m_loopArea = (start, end);
        }

        public void RemoveLoopArea() => m_loopArea = null;

        public void SetLoopArea(time_t start, time_t end)
        {
            SetLoopAreaSamples(TimeToSamples(start), TimeToSamples(end));
        }

        public override int Read(Span<float> buffer)
        {
            if (Source == null) return 0;

            int count = buffer.Length;
            switch (PlaybackState)
            {
                case PlaybackState.Playing:
                {
                    if (m_loopArea is { } area)
                        m_isLoopingArea = m_realSampleIndex >= area.Repeat && m_realSampleIndex <= area.Check;
                    else m_isLoopingArea = false;

                    m_lastSourcePosition = m_realSampleIndex / (double)(Source.Format.SampleRate * Source.Format.ChannelCount);

                    int realSampleCount = (int)(count * m_playbackSpeed);
                    m_resampleBuffer = m_resampleBuffer.CheckBuffer(realSampleCount);
                    
                    float LerpSample(float[] arr, double index)
                    {
                        index = MathL.Clamp(index, 0, arr.Length);
                        if (index == 0) return arr[0];
                        if (index == arr.Length) return arr[arr.Length - 1];
                        int min = (int)index, max = min + 1;
                        return MathL.Lerp(arr[min], arr[max], (float)(index - min));
                    }

                    int numEmptySamples = (int)(MathL.Clamp(-(int)m_realSampleIndex, 0, count) * m_playbackSpeed);
                    for (int e = 0; e < numEmptySamples; e++)
                        m_resampleBuffer[e] = 0;

                    int numReadSamples, ifLoopingSampleCount;
                    if (m_isLoopingArea && realSampleCount > (ifLoopingSampleCount =
                        (int)(m_loopArea!.Value.Check - (m_realSampleIndex + numEmptySamples))))
                    {
                        int numReadAtEnd = Source.Read(m_resampleBuffer.AsSpan(numEmptySamples, ifLoopingSampleCount));

                        m_realSampleIndex = m_loopArea!.Value.Repeat;
                        time_t loopBackPointSeconds = (double)m_realSampleIndex / (Source.Format.SampleRate * Source.Format.ChannelCount);
                        Source!.Position = m_lastSourcePosition = loopBackPointSeconds;

                        int numReadAtLoop = Source.Read(m_resampleBuffer.AsSpan(numEmptySamples + numReadAtEnd, ifLoopingSampleCount - numReadAtEnd));

                        numReadSamples = numReadAtEnd + numReadAtLoop;
                    }
                    else numReadSamples = Source.Read(m_resampleBuffer.AsSpan(numEmptySamples, realSampleCount - numEmptySamples));
                    int totalSamplesRead = numReadSamples + numEmptySamples;

                    int numSamplesToWrite = (int)(totalSamplesRead * m_invPlaybackSpeed);
                    for (int i = 0; i < numSamplesToWrite; i++)
                        buffer[i] = LerpSample(m_resampleBuffer, i * m_playbackSpeed) * Volume;

                    m_realSampleIndex += totalSamplesRead;
                    return numSamplesToWrite;
                }

                case PlaybackState.Stopped:
                    for (int i = 0; i < count; i++)
                        buffer[i] = 0;
                    return count;

                default: return 0;
            }
        }

        public override void Seek(time_t position)
        {
            if (!CanSeek) throw new InvalidOperationException("cannot seek");
            
            double posSeconds = MathL.Max(0, position.Seconds);
            Source!.Position = posSeconds;

            m_lastSourcePosition = position.Seconds;
            m_realSampleIndex = (long)(position.Seconds * Source.Format.SampleRate * Source.Format.ChannelCount);
        }

        protected override void DisposeManaged()
        {
            Channel = null;
            Source?.Dispose();
        }
    }
}
