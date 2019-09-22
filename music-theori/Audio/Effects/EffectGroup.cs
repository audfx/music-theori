using System;
using System.Collections.Generic;

using theori.Charting.Effects;

namespace theori.Audio.Effects
{
    public sealed class EffectGroup : Dsp
    {
        private readonly Dsp[] m_effects;
        
        private readonly List<float[]> m_buffers;
        private readonly float m_average;

        public EffectGroup(int sampleRate, EffectDef[] effects)
           : base(sampleRate)
        {
            m_effects = new Dsp[effects.Length].Fill(i => effects[i].CreateEffectDsp(sampleRate));
            m_buffers = new List<float[]>();
            for (int i = 0; i < effects.Length; i++) m_buffers[0] = new float[256];
            m_average = 1.0f / effects.Length;
        }

        protected override void ProcessImpl(Span<float> buffer)
        {
            for (int i = 0; i < m_buffers.Count; i++)
            {
                m_buffers[i] = m_buffers[i].CheckBuffer(buffer.Length);
                buffer.CopyTo(m_buffers[i]);

                m_effects[i].Process(m_buffers[i].AsSpan(0, buffer.Length));
            }

            buffer.Fill(0);
            for (int n = 0; n < buffer.Length; n++)
            {
                for (int i = 0; i < m_buffers.Count; i++)
                    buffer[n] += m_buffers[i][n];
                buffer[n] *= Mix * m_average;
            }
        }
    }
}
