using System;

namespace theori.Audio.Effects
{
    public class Wobble : Dsp
    {
	    private static readonly CubicBezier easing = new CubicBezier(Ease.InExpo);

        private BiQuadFilter filter;

	    // Frequency range
	    float fmin = 500.0f;
	    float fmax = 20000.0f;
	    float q = 1.414f;

        private int m_currentSample, m_length;

        public Wobble(int sampleRate)
            : base(sampleRate)
        {
            filter = new BiQuadFilter(sampleRate);
            filter.Mix = 1.0f;
        }
        
        public void SetPeriod(double period)
        {
	        m_length = (int)(period * SampleRate);
        }

        protected override void ProcessImpl(Span<float> buffer)
        {
            int numSamples = buffer.Length / 2;
            
	        for(int i = 0; i < numSamples; i++)
	        {
		        float f = MathL.Abs(2.0f * ((float)m_currentSample / m_length) - 1.0f);
		        f = easing.Sample(f);
		        float freq = fmin + (fmax - fmin) * f;
		        filter.SetLowPass(q, freq);

		        float[] s = { buffer[i * 2], buffer[i * 2 + 1] };
		        filter.Process(s);

		        float addMix = 0.85f;
                buffer[i * 2 + 0] = MathL.Lerp(buffer[i * 2 + 0], s[0], Mix * addMix);
                buffer[i * 2 + 1] = MathL.Lerp(buffer[i * 2 + 1], s[1], Mix * addMix);

                m_currentSample++;
		        m_currentSample %= m_length;
	        }
        }
    }
}
