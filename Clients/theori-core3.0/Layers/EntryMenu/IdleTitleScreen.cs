using System;

using theori.Graphics;
using theori.IO;

namespace theori.Core30.Layers.EntryMenu
{
    public sealed class IdleTitleScreen : Layer
    {
        private BasicSpriteRenderer? m_renderer;

        private float m_startTextAlpha = 1.0f;

        private float m_animTimerWhen = 0.0f;
        private float m_animTimer = 0.0f;

        public IdleTitleScreen()
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
            m_animTimerWhen = Time.Total;
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

            m_animTimerWhen = Time.Total;

            ClientAs<TheoriClient>().OpenCurtain();
        }

        public override bool KeyPressed(KeyInfo info)
        {
            switch (info.KeyCode)
            {
                case KeyCode.ESCAPE: ClientAs<TheoriClient>().CloseCurtain(() => Host.Exit()); break;

                case KeyCode.F9:
                {
                    ClientAs<TheoriClient>().CloseCurtain(() => Push(new AttractModeLayer()));
                } break;

                default: return false;
            }

            return true;
        }

        public override void Update(float delta, float total)
        {
            m_animTimer = total - m_animTimerWhen;
            m_startTextAlpha = MathL.Abs(MathL.Sin(m_animTimer * 3));
        }

        public override void Render()
        {
            var r = m_renderer!;

            r.BeginFrame();
            r.SetColor(255, 255, 255, 255 * m_startTextAlpha);
            r.SetTextAlign(Anchor.MiddleCenter);
            r.SetFontSize(48);
            r.Write("Press Start", Window.Width / 2, Window.Height * 2 / 3);
            r.EndFrame();
        }
    }
}
