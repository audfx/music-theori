using System;

using theori.Charting.Effects;

namespace theori.Audio.Effects
{
    public sealed class EffectChain : Dsp
    {
        private readonly Dsp[] m_effects;

        public EffectChain(int sampleRate, EffectDef[] effects)
            : base(sampleRate)
        {
            m_effects = new Dsp[effects.Length].Fill(i => effects[i].CreateEffectDsp(sampleRate));
        }

        protected override void ProcessImpl(Span<float> buffer)
        {
            foreach (var effect in m_effects)
                effect.Process(buffer);
        }
    }
}
