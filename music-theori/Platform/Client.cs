using System;

using theori.Database;
using theori.Graphics;
using theori.IO;

namespace theori.Platform
{
    public abstract class Client
    {
        // yes, this effectively is a nullable type signature considering the exception throw.
        // It's more intended as a debug feature, Host should ALWAYS be set properly during initialization, but initialization isn't always during construction.
        private ClientHost? m_host = null;
        public ClientHost Host => m_host ?? throw new InvalidOperationException("No host has been assigned to this client.");

        public ChartDatabaseWorker DatabaseWorker { get; private set; }

        internal LayerStack LayerStack { get; private set; }

        protected TransitionCurtain Curtain { get; }

        public long TargetFrameTimeMillis
        {
            get
            {
                long millis = LayerStack.CurrentTargetFrameTimeMillis;
                return millis == 0 ? 16 : millis;
            }
        }

        [Pure] protected Client()
        {
            DatabaseWorker = new ChartDatabaseWorker();
            Curtain = CreateTransitionCurtain();

            LayerStack = new LayerStack(this, CreateInitialLayer());
            UserInputService.Initialize(LayerStack);
        }

        /// <summary>
        /// Used to construct an initial layer, if any.
        /// If this is not used then a starting layer must be added manually.
        /// </summary>
        [Const] protected virtual Layer? CreateInitialLayer() => null;
        [Const] protected virtual TransitionCurtain CreateTransitionCurtain() => new TransitionCurtain();

        public virtual void SetHost(ClientHost host)
        {
            if (m_host != null) throw new InvalidOperationException("Client has already been assigned a host.");
            m_host = host;
        }

        protected internal virtual UnhandledExceptionAction OnUnhandledException()
        {
            return UnhandledExceptionAction.SafeExit;
        }

        public bool CloseCurtain(float holdTime, Action? onClosed = null) => Curtain!.Close(holdTime, onClosed);
        public bool CloseCurtain(Action? onClosed = null) => Curtain!.Close(0.5f, onClosed);
        public bool OpenCurtain(Action? onOpened = null) => Curtain!.Open(onOpened);

        protected internal virtual void BeginFrame()
        {
            LayerStack.BeginFrame();
        }

        /// <summary>
        /// Called *before* input devices are polled and state is updated.
        /// </summary>
        protected internal virtual void BeginInputStep()
        {
            LayerStack.BeginInputStep();
        }

        /// <summary>
        /// Called *after* input devices are polled and state is updated.
        /// </summary>
        protected internal virtual void EndInputStep()
        {
            LayerStack.EndInputStep();
        }

        #region Update

        /// <summary>
        /// Called at the start of the update step, after host-level update events occur.
        /// </summary>
        protected internal virtual void BeginUpdateStep()
        {
            LayerStack.BeginUpdateStep();
        }

        /// <summary>
        /// Called at the start of the update step, after host-level update events occur.
        /// </summary>
        protected internal virtual void FixedUpdate(float fixedDelta, float totalTime)
        {
            LayerStack.FixedUpdate(fixedDelta, totalTime);
        }

        /// <summary>
        /// </summary>
        protected internal virtual void Update(float varyingDelta, float totalTime)
        {
            LayerStack.Update(varyingDelta, totalTime);
        }

        /// <summary>
        /// Called at the end of the update set.
        /// </summary>
        protected internal virtual void LateUpdate()
        {
            LayerStack.LateUpdate();
        }

        /// <summary>
        /// </summary>
        protected internal virtual void EndUpdateStep()
        {
            LayerStack.EndUpdateStep();
        }

        #endregion

        #region Render

        /// <summary>
        /// Called at the beginning of the render step.
        /// </summary>
        protected internal virtual void BeginRenderStep()
        {
            LayerStack.BeginRenderStep();
        }

        /// <summary>
        /// </summary>
        protected internal virtual void Render()
        {
            LayerStack.Render();
        }

        /// <summary>
        /// </summary>
        protected internal virtual void LateRender()
        {
            LayerStack.LateRender();
        }

        /// <summary>
        /// Called at the end of the render step, before the window is updated.
        /// </summary>
        protected internal virtual void EndRenderStep()
        {
            LayerStack.EndRenderStep();
        }

        #endregion
    }
}
