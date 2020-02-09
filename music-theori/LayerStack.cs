using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using MoonSharp.Interpreter;

using theori.IO;
using theori.Platform;
using theori.Scripting;

namespace theori
{
    public sealed class LayerStack
    {
        static LayerStack()
        {
            ScriptService.RegisterType<LayerStack>();
        }

        [MoonSharpHidden]
        public long CurrentTargetFrameTimeMillis => m_layers.TryPeek(out var layer) ? layer.TargetFrameRate == 0 ? 0 : 1000 / layer.TargetFrameRate : 0;

        private readonly Client m_owner;

        private InputService InputService => m_owner.ScriptExecutionEnvironment.InputService;

        private readonly Stack<Layer> m_layers = new Stack<Layer>();

        private readonly Queue<Action> m_scheduled = new Queue<Action>();
        private readonly List<(Task<bool> Task, Action Action)> m_loadOps = new List<(Task<bool> Task, Action Action)>();

        public Layer? Top => m_layers.TryPeek(out var top) ? top : null;
        public int Count => m_layers.Count;

        internal LayerStack(Client owner, Layer? initialLayer = null)
        {
            m_owner = owner;
            if (initialLayer != null) Push(null, initialLayer!);
        }

        [MoonSharpHidden]
        public bool Push(Layer? source, Layer nextLayer)
        {
            if (source == null && m_layers.Count > 0)
                throw new InvalidOperationException("A non-empty stack must have a source layer.");

            nextLayer.SetClient(m_owner);
            Schedule(() => LoadLayer(source, nextLayer, () => FinishPush(source, nextLayer)));

            return true;
        }

        public bool Push(Layer nextLayer) => Push(Top, nextLayer);

        [MoonSharpHidden]
        public void Pop(Layer? source)
        {
            if (source == null || !m_layers.Contains(source))
                throw new InvalidOperationException("Given layer is not in the layer stack.");

            if (m_layers.Count == 0 || m_layers.Peek() != source)
                throw new InvalidOperationException("Given layer is not the current layer.");

            Schedule(() => PopFrom(source));
        }

        public void PopTop() => Pop(Top);

        private void PopFrom(Layer? source)
        {
            if (m_layers.Count == 0)
                return;

            var toRemove = m_layers.Peek();
            if (toRemove.OnExiting(source)) return;

            m_layers.Pop(); // = toRemove

            toRemove.validForResume = false;
            toRemove.OnDestroy(); // TODO(local): queue destroys?

            ResumeFrom(source);
        }

        private void ResumeFrom(Layer? source)
        {
            if (m_layers.Count == 0) return;

            var toResume = m_layers.Peek();
            if (!toResume.validForResume)
                PopFrom(source);
            else m_layers.Peek().ResumeInternal(source!);
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

        [MoonSharpHidden]
        public void BeginInputStep()
        {
        }

        internal void KeyPressed(KeyInfo info) { InputService.OnKeyPressed(info); if (m_layers.Count > 0) m_layers.Peek()!.KeyPressed(info); }
        internal void KeyReleased(KeyInfo info) { InputService.OnKeyReleased(info); if (m_layers.Count > 0) m_layers.Peek()!.KeyReleased(info); }

        internal void MouseButtonPressed(MouseButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.MouseButtonPressed(info); }
        internal void MouseButtonReleased(MouseButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.MouseButtonReleased(info); }
        internal void MouseWheelScrolled(int x, int y) { if (m_layers.Count > 0) m_layers.Peek()!.MouseWheelScrolled(x, y); }
        internal void MouseMoved(int x, int y, int dx, int dy) { if (m_layers.Count > 0) m_layers.Peek()!.MouseMoved(x, y, dx, dy); }

        internal void GamepadConnected(Gamepad gamepad) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadConnected(gamepad); }
        internal void GamepadDisconnected(Gamepad gamepad) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadDisconnected(gamepad); }
        internal void GamepadButtonPressed(GamepadButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadButtonPressed(info); }
        internal void GamepadButtonReleased(GamepadButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadButtonReleased(info); }
        internal void GamepadAxisChanged(GamepadAxisInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadAxisChanged(info); }
        internal void GamepadBallChanged(GamepadBallInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadBallChanged(info); }

        internal void ControllerAdded(Controller controller) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerAdded(controller); }
        internal void ControllerRemoved(Controller controller) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerRemoved(controller); }
        internal void ControllerButtonPressed(ControllerButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerButtonPressed(info); }
        internal void ControllerButtonReleased(ControllerButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerButtonReleased(info); }
        internal void ControllerAxisChanged(ControllerAxisInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerAxisChanged(info); }
        internal void ControllerAxisTicked(ControllerAxisTickInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerAxisTicked(info); }

        [MoonSharpHidden]
        public void EndInputStep()
        {
        }

        [MoonSharpHidden]
        public void BeginUpdateStep()
        {
        }

        [MoonSharpHidden]
        public void FixedUpdate(float fixedDelta, float totalTime)
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.FixedUpdate(fixedDelta, totalTime);
        }

        [MoonSharpHidden]
        public void Update(float varyingDelta, float totalTime)
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.Update(varyingDelta, totalTime);
        }

        [MoonSharpHidden]
        public void LateUpdate()
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.LateUpdate();
        }

        [MoonSharpHidden]
        public void EndUpdateStep()
        {
        }

        [MoonSharpHidden]
        public void BeginRenderStep()
        {
        }

        [MoonSharpHidden]
        public void Render()
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.Render();
        }

        [MoonSharpHidden]
        public void LateRender()
        {
            if (m_layers.TryPeek(out var topLayer))
                topLayer.LateRender();
        }

        [MoonSharpHidden]
        public void EndRenderStep()
        {
        }
    }
}
