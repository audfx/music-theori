using System;

using theori.Core30.Layers;

using theori.Graphics;
using theori.Platform;

namespace theori.Core30
{
    internal class TheoriClient : Client
    {
        private TransitionCurtain? m_curtain;

        [Pure] public TheoriClient()
        {
        }

        [Const] protected override Layer? CreateInitialLayer() => new SplashScreen();

        protected override UnhandledExceptionAction OnUnhandledException()
        {
            return UnhandledExceptionAction.GiveUpRethrow;
        }

        public override void SetHost(ClientHost host)
        {
            base.SetHost(host);

            theori.Host.StaticResources.AquireTexture("textures/theori-logo-large");
            theori.Host.StaticResources.AquireTexture("textures/audfx-text-large");

            m_curtain = new TransitionCurtain();
        }

        public bool CloseCurtain(float holdTime, Action? onClosed = null) => m_curtain!.Close(holdTime, onClosed);
        public bool CloseCurtain(Action? onClosed = null) => m_curtain!.Close(0.2f, onClosed);
        public bool OpenCurtain(Action? onOpened = null) => m_curtain!.Open(onOpened);

        protected override void Update(float varyingDelta, float totalTime)
        {
            base.Update(varyingDelta, totalTime);

            m_curtain?.Update(varyingDelta, totalTime);
        }

        protected override void FixedUpdate(float fixedDelta, float totalTime)
        {
            base.FixedUpdate(fixedDelta, totalTime);
        }

        protected override void EndRenderStep()
        {
            m_curtain?.Render();
            base.EndRenderStep();
        }
    }
}
