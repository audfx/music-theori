using System;

namespace theori.Audio
{
    public abstract class AudioSource : Disposable
    {
        public event Action? Finish;

        public abstract bool CanSeek { get; }

        public abstract int SampleRate { get; }
        public abstract int Channels { get; }

        public abstract time_t Position { get; set; }
        public abstract time_t Length { get; }

        private float m_volume = 1.0f;
        public virtual float Volume
        {
            get => m_volume;
            set => m_volume = MathL.Clamp(value, 0, 1);
        }

        internal void OnFinish() => Finish?.Invoke();

        public virtual bool RemoveFromChannelOnFinish { get; set; } = true;

        public abstract int Read(Span<float> buffer);
        public abstract void Seek(time_t position);

        private MixerChannel? m_channel;
        public MixerChannel? Channel
        {
            get => m_channel;
            set
            {
                if (m_channel == value) return;
                if (m_channel != null)
                {
                    m_channel.RemoveSource(this);
                    m_channel.OnSampleSourceEnded -= OnRemoveFromMixerChannelEvent;
                }

                m_channel = value;
                if (m_channel != null)
                {
                    m_channel.AddSource(this);
                    m_channel.OnSampleSourceEnded += OnRemoveFromMixerChannelEvent;
                }
            }
        }

        private void OnRemoveFromMixerChannelEvent(AudioSource track) => OnRemoveFromMixerChannel();
        protected virtual void OnRemoveFromMixerChannel()
        {
        }
    }
}
