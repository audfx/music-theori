using System;

using theori.Audio;
using theori.Audio.Effects;

namespace theori.Charting.Effects
{
    [EffectType("EffectChain")]
    public sealed class EffectChainDef : EffectDef
    {
        [TheoriProperty("effects")]
        public EffectDef[]? Effects;

        public EffectChainDef() : base(1) { }

        public EffectChainDef(EffectParamF mix, EffectDef[] effects)
            : base(mix)
        {
            Effects = effects;
        }

        public override Dsp CreateEffectDsp(int sampleRate)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(EffectDef other)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
