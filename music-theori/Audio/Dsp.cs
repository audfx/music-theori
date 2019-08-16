using System;

namespace theori.Audio
{
    public abstract class Dsp
    {
        public int SampleRate { get; }

        public float Mix { get; set; } = 0.5f;

        protected Dsp(int sampleRate)
        {
            SampleRate = sampleRate;
        }

        public virtual void Reset() { }

        public void Process(Span<float> buffer) => ProcessImpl(buffer);
        protected abstract void ProcessImpl(Span<float> buffer);
    }
}
