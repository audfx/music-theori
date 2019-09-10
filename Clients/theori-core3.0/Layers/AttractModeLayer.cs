using System;

using theori.Graphics;
using theori.IO;

namespace theori.Core30.Layers
{
    internal sealed class AttractModeLayer : Layer
    {
        private BasicSpriteRenderer? m_renderer;

        public AttractModeLayer()
        {
        }

        public override bool AsyncLoad()
        {
            return true;
        }

        public override bool AsyncFinalize()
        {
            return true;
        }

        public override void Initialize()
        {
            m_renderer = new BasicSpriteRenderer();

            ClientAs<TheoriClient>().OpenCurtain();
        }

        public override void Destroy()
        {
            m_renderer?.Dispose();
            m_renderer = null;
        }

        public override void Resumed(Layer previousLayer)
        {
            base.Resumed(previousLayer);

            ClientAs<TheoriClient>().OpenCurtain();
        }

        public override bool KeyPressed(KeyInfo info)
        {
            switch (info.KeyCode)
            {
                case KeyCode.ESCAPE: ClientAs<TheoriClient>().CloseCurtain(() => Pop()); break;

                default: return false;
            }

            return true;
        }

        public override void Update(float delta, float total)
        {
        }

        public override void Render()
        {
        }
    }
}
