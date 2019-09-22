using System;

using theori.Audio;
using theori.Audio.Effects;

namespace theori.Charting.Effects
{
    [EffectType("EffectGroup")]
    public sealed class EffectGroupDef : EffectDef
    {
        [TheoriProperty("effects")]
        public EffectDef[]? Effects;

        public EffectGroupDef() : base(1) { }

        public EffectGroupDef(EffectParamF mix, EffectDef[] effects)
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
