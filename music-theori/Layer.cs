using System;
using System.Numerics;

using theori.Graphics;
using theori.Gui;
using theori.IO;
using theori.Platform;
using theori.Resources;

namespace theori
{
    public abstract class Layer : IKeyboardListener, IAsyncLoadable
    {
        internal enum LifetimeState
        {
            Uninitialized,
            Queued,
            Alive,
            Destroyed,
        }

        internal enum LoadState
        {
            Unloaded,
            AsyncLoading,
            AwaitingFinalize,
            Loaded,
            Destroyed,
        }

        internal LifetimeState lifetimeState = LifetimeState.Uninitialized;
        internal LoadState loadState = LoadState.Unloaded;

        private Client? m_client = null;
        public Client Client => m_client ?? throw new InvalidOperationException("Layer has not been initialized with a client yet.");

        public T ClientAs<T>() where T : Client => (T)Client;

        public ClientHost Host => Client.Host;

        public T HostAs<T>() where T : ClientHost => (T)Host;

        public virtual int TargetFrameRate => 0;

        public virtual bool BlocksParentLayer => true;

        public bool IsSuspended { get; private set; } = false;

        internal bool validForResume = true;

        internal void SetClient(Client client)
        {
            if (m_client != null) throw new InvalidOperationException("Layer already has a client assigned to it.");
            m_client = client;
        }

        internal void InitializeInternal()
        {
            Initialize();
            lifetimeState = LifetimeState.Alive;
        }

        internal void DestroyInternal()
        {
            lifetimeState = LifetimeState.Destroyed;
            Destroy();
        }

        internal void SuspendInternal(Layer nextLayer)
        {
            if (IsSuspended) return;
            IsSuspended = true;

            Suspended(nextLayer);
        }

        internal void ResumeInternal(Layer previousLayer)
        {
            if (!IsSuspended) return;
            IsSuspended = false;

            Resumed(previousLayer);
        }

        protected void Push(Layer nextLayer) => Client.LayerStack.Push(this, nextLayer);
        protected void Pop() => Client.LayerStack.Pop(this);

        protected void SetInvalidForResume() => validForResume = false;

        /// <summary>
        /// Called whenever the client window size is changed, even if this layer is suspended.
        /// If being suspended is important, check <see cref="IsSuspended"/>.
        /// </summary>
        public virtual void ClientSizeChanged(int width, int height) { }

        public virtual bool AsyncLoad() { return true; }
        public virtual bool AsyncFinalize() { return true; }

        public virtual void Initialize() { }
        public virtual void Destroy() { }

        /// <summary>
        /// Called when another layer which hides lower layers is placed anywhere above this layer.
        /// This will not be called if the layer is already in a suspended state.
        /// This will be called at the beginning of a frame, before inputs and updates, allowing the layer to
        ///  properly pause or destroy state after completing a frame.
        /// </summary>
        public virtual void Suspended(Layer nextLayer) { }
        /// <summary>
        /// Called when another layer which hides lower layers is removed from anywhere above this layer.
        /// This will not be invoked if this layer is already in an active state.
        /// </summary>
        public virtual void Resumed(Layer previousLayer) { }
        /// <summary>
        /// Returns true to cancel the exit, false to continue.
        /// </summary>
        public virtual bool OnExiting(Layer? source) => false;

        public virtual bool KeyPressed(KeyInfo info) => false;
        public virtual bool KeyReleased(KeyInfo info) => false;

        public virtual bool MouseButtonPressed(MouseButtonInfo info) => false;
        public virtual bool MouseButtonReleased(MouseButtonInfo info) => false;
        public virtual bool MouseWheelScrolled(int x, int y) => false;
        public virtual bool MouseMoved(int x, int y, int dx, int dy) => false;

        public virtual void Update(float delta, float total) { }
        public virtual void FixedUpdate(float delta, float total) { }
        public virtual void LateUpdate() { }

        public virtual void Render() { }
        public virtual void LateRender() { }
    }

    public abstract class Overlay : Layer
    {
        public sealed override int TargetFrameRate => 0;

        public sealed override void Suspended(Layer nextLayer) { }
        public sealed override void Resumed(Layer previousLayer) { }
    }

    public class GenericTransitionLayer : Layer
    {
        private readonly Layer m_nextLayer;
        private readonly BasicSpriteRenderer m_renderer;

        private AsyncLoader m_loader = new AsyncLoader();

        public GenericTransitionLayer(Layer nextLayer, ClientResourceLocator locator)
        {
            m_nextLayer = nextLayer;
            m_renderer = new BasicSpriteRenderer(locator ?? ClientResourceLocator.Default, new Vector2(Window.Width, Window.Height));
        }

        public override void Destroy()
        {
            m_renderer.Dispose();
        }

        public override void Initialize()
        {
            m_loader.Add(m_nextLayer);
            m_loader.LoadAll();
        }

        public override void Update(float delta, float total)
        {
            if (m_loader == null) return;

            m_loader.Update();
            if (m_loader.Failed)
                ;// Host.RemoveLayer(this);
            else if (m_loader.IsCompleted)
            {
                if (m_loader.IsFinalizeSuccessful)
                    ;//Host.AddLayerAbove(this, m_nextLayer);
                ;//Host.RemoveLayer(this);
            }
        }

        public override void Render()
        {
            m_renderer.BeginFrame();
            m_renderer.Translate(-50, -50);
            m_renderer.Rotate(Time.Total * 180);
            m_renderer.Translate(Window.Width / 2.0f, Window.Height / 2.0f);
            m_renderer.FillRect(0, 0, 100, 100);
            m_renderer.Flush();
            m_renderer.EndFrame();
        }
    }
}
