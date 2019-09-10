using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using theori.IO;
using theori.Platform;

namespace theori
{
    public sealed class LayerStack : IKeyboardListener
    {
        public long CurrentTargetFrameTimeMillis => m_layers.TryPeek(out var layer) ? layer.TargetFrameRate == 0 ? 0 : 1000 / layer.TargetFrameRate : 0;

        private readonly Client m_owner;

        private readonly Stack<Layer> m_layers = new Stack<Layer>();

        private readonly Queue<Action> m_scheduled = new Queue<Action>();
        private readonly List<(Task<bool> Task, Action Action)> m_loadOps = new List<(Task<bool> Task, Action Action)>();

        internal LayerStack(Client owner, Layer? initialLayer = null)
        {
            m_owner = owner;
            if (initialLayer != null) Push(null, initialLayer!);
        }

        public bool Push(Layer? source, Layer nextLayer)
        {
            if (source == null && m_layers.Count > 0)
                throw new InvalidOperationException("A non-empty stack must have a source layer.");

            nextLayer.SetClient(m_owner);
            Schedule(() => LoadLayer(source, nextLayer, () => FinishPush(source, nextLayer)));

            return true;
        }

        public void Pop(Layer? source)
        {
            if (source == null || !m_layers.Contains(source))
                throw new InvalidOperationException("Given layer is not in the layer stack.");

            if (m_layers.Count == 0 || m_layers.Peek() != source)
                throw new InvalidOperationException("Given layer is not the current layer.");

            Schedule(() => PopFrom(source));
        }

        private void PopFrom(Layer? source)
        {
            if (m_layers.Count == 0)
                return;

            var toRemove = m_layers.Peek();
            if (toRemove.OnExiting(source)) return;

            m_layers.Pop(); // = toRemove

            toRemove.validForResume = false;
            toRemove.Destroy(); // TODO(local): queue destroys?

            ResumeFrom(source);
        }

        private void ResumeFrom(Layer? source)
        {
            if (m_layers.Count == 0) return;

            m_layers.Peek().ResumeInternal(source!);
        }

        private void Schedule(Action action) => m_scheduled.Enqueue(action);

        private void LoadLayer(Layer? source, Layer toLoad, Action continuation)
        {
            if (source?.lifetimeState == Layer.LifetimeState.Destroyed)
                return;

            if (toLoad.loadState >= Layer.LoadState.Loaded)
                continuation();
            else
            {
                if (source == null || source!.loadState >= Layer.LoadState.Loaded)
                    LoadLayerAsync(source, toLoad, continuation);
                else Schedule(() => LoadLayer(source, toLoad, continuation));
            }
        }

        private void LoadLayerAsync(Layer? source, Layer toLoad, Action continuation)
        {
            var loadOp = Task.Run(() =>
            {
                toLoad.loadState = Layer.LoadState.AsyncLoading;
                bool result = toLoad.AsyncLoad();
                if (result) toLoad.loadState = Layer.LoadState.AwaitingFinalize;
                return result;
            });
            m_loadOps.Add((loadOp, continuation));
        }

        private void FinishPush(Layer? source, Layer nextLayer)
        {
            if (nextLayer.loadState < Layer.LoadState.AwaitingFinalize)
                return;

            if (nextLayer.loadState == Layer.LoadState.AwaitingFinalize)
            {
                if (!nextLayer.AsyncFinalize())
                    return;

                nextLayer.loadState = Layer.LoadState.Loaded;
            }

            source?.SuspendInternal(nextLayer);

            m_layers.Push(nextLayer);
            nextLayer.InitializeInternal();
        }

        internal void BeginFrame()
        {
            while (m_scheduled.TryDequeue(out var action))
                action();

            for (int i = 0; i < m_loadOps.Count; i++)
            {
                var (task, action) = m_loadOps[i];
                if (task.IsCompletedSuccessfully)
                {
                    if (task.Result) action();
                    m_loadOps.RemoveAt(i--);
                }
            }
        }

        public void BeginInputStep()
        {
        }

        bool IKeyboardListener.KeyPressed(KeyInfo info) => m_layers.Peek()!.KeyPressed(info);

        bool IKeyboardListener.KeyReleased(KeyInfo info) => m_layers.Peek()!.KeyReleased(info);

        public void EndInputStep()
        {
        }

        public void BeginUpdateStep()
        {
        }

        public void FixedUpdate(float fixedDelta, float totalTime)
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.FixedUpdate(fixedDelta, totalTime);
        }

        public void Update(float varyingDelta, float totalTime)
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.Update(varyingDelta, totalTime);
        }

        public void LateUpdate()
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.LateUpdate();
        }

        public void EndUpdateStep()
        {
        }

        public void BeginRenderStep()
        {
        }

        public void Render()
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.Render();
        }

        public void LateRender()
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.LateRender();
        }

        public void EndRenderStep()
        {
        }
    }
}
