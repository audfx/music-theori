using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using theori.IO;
using theori.Platform;

namespace theori
{
    public sealed class LayerStack
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

        public void BeginInputStep()
        {
        }

        internal void TextInput(string composition) { if (m_layers.Count > 0) m_layers.Peek()!.TextInput(composition); }

        internal void KeyPressed(KeyInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.KeyPressed(info); }
        internal void KeyReleased(KeyInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.KeyReleased(info); }
        internal void RawKeyPressed(KeyInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.RawKeyPressed(info); }
        internal void RawKeyReleased(KeyInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.RawKeyReleased(info); }

        internal void MouseButtonPressed(MouseButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.MouseButtonPressed(info); }
        internal void MouseButtonReleased(MouseButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.MouseButtonReleased(info); }
        internal void MouseWheelScrolled(int x, int y) { if (m_layers.Count > 0) m_layers.Peek()!.MouseWheelScrolled(x, y); }
        internal void MouseMoved(int x, int y, int dx, int dy) { if (m_layers.Count > 0) m_layers.Peek()!.MouseMoved(x, y, dx, dy); }
        internal void RawMouseButtonPressed(MouseButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.RawMouseButtonPressed(info); }
        internal void RawMouseButtonReleased(MouseButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.RawMouseButtonReleased(info); }
        internal void RawMouseWheelScrolled(int x, int y) { if (m_layers.Count > 0) m_layers.Peek()!.RawMouseWheelScrolled(x, y); }
        internal void RawMouseMoved(int x, int y, int dx, int dy) { if (m_layers.Count > 0) m_layers.Peek()!.RawMouseMoved(x, y, dx, dy); }

        internal void GamepadConnected(Gamepad gamepad) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadConnected(gamepad); }
        internal void GamepadDisconnected(Gamepad gamepad) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadDisconnected(gamepad); }
        internal void GamepadButtonPressed(GamepadButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadButtonPressed(info); }
        internal void GamepadButtonReleased(GamepadButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadButtonReleased(info); }
        internal void GamepadAxisChanged(GamepadAxisInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadAxisChanged(info); }
        internal void GamepadBallChanged(GamepadBallInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.GamepadBallChanged(info); }
        internal void RawGamepadButtonPressed(GamepadButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.RawGamepadButtonPressed(info); }
        internal void RawGamepadButtonReleased(GamepadButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.RawGamepadButtonReleased(info); }
        internal void RawGamepadAxisChanged(GamepadAxisInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.RawGamepadAxisChanged(info); }
        internal void RawGamepadBallChanged(GamepadBallInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.RawGamepadBallChanged(info); }

        internal void ControllerAdded(Controller controller) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerAdded(controller); }
        internal void ControllerRemoved(Controller controller) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerRemoved(controller); }
        internal void ControllerButtonPressed(ControllerButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerButtonPressed(info); }
        internal void ControllerButtonReleased(ControllerButtonInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerButtonReleased(info); }
        internal void ControllerAxisChanged(ControllerAxisInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerAxisChanged(info); }
        internal void ControllerAxisTicked(ControllerAxisTickInfo info) { if (m_layers.Count > 0) m_layers.Peek()!.ControllerAxisTicked(info); }

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
