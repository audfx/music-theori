using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Threading;

using theori.Audio;
using theori.Configuration;
using theori.Graphics;
using theori.Graphics.OpenGL;
using theori.IO;
using theori.Scripting;

namespace theori.Platform
{
    public enum UnhandledExceptionAction
    {
        SafeExit,
        DoNothing,
        GiveUpRethrow,
    }

    public abstract class ClientHost : Disposable
    {
        private const string GAME_CONFIG_FILE = "theori-config.ini";

        public event Action? Activated;
        public event Action? Deactivated;
        public event Action? Exited;

        private readonly Queue<Action> m_updateQueue = new Queue<Action>();

        private readonly ManualResetEventSlim m_stoppedEvent = new ManualResetEventSlim(false);

        protected ClientHost()
        {
        }

        public virtual void Initialize()
        {
            using var _ = Profiler.Scope("ClientHost::Initialize");
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            LoadConfig();

            Window.Create(this);
            Window.VSync = TheoriConfig.VerticalSync;
            Window.ClientSizeChanged += Window_ClientSizeChanged;

            Mixer.Initialize(new AudioFormat(48000, 2));

            ScriptService.RegisterTheoriClrTypes();

            Logger.Log($"Window VSync: { Window.VSync }");

            UserInputService.RawKeyPressed += (keyInfo) =>
            {
                if (keyInfo.KeyCode == KeyCode.F12)
                    m_updateQueue.Enqueue(() =>
                    {
                        Profiler.IsEnabled = true;
                        Profiler.BeginSession("Single Frame Session");
                    });
            };
        }

        protected override void DisposeManaged()
        {
            m_stoppedEvent.Wait();
            m_stoppedEvent.Dispose();
        }

        public void Run(Client client)
        {
            //Profiler.BeginSession("Main Game Process");

            void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
            {
                switch (client.OnUnhandledException())
                {
                    case UnhandledExceptionAction.SafeExit:
                    {
                        if (e.ExceptionObject is Exception ex)
                        {
                            Logger.Log(ex.Message);
                            Logger.Log(ex.StackTrace);
                        }
                        else Logger.Log("Unknown exception thrown?");

                        PerformExit(true);
                    } break;

                    case UnhandledExceptionAction.DoNothing: break; // oh, okay

                    case UnhandledExceptionAction.GiveUpRethrow:
                    {
                        //Profiler.EndSession();
                        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
                        throw (Exception)e.ExceptionObject;
                    } // break;
                }
            }

            try
            {
                AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

                Window.Update();
                client.SetHost(this);

                var timer = Stopwatch.StartNew();
                long lastFrameStart = timer.ElapsedMilliseconds;

                long accumulatedTimeMillis = 0;

                while (true)
                {
                    using var _ = Profiler.Scope("Game Loop");

                    long currentTimeMillis = timer.ElapsedMilliseconds;
                    //Time.HighResolution = timer.ElapsedTicks * 1_000_000L / Stopwatch.Frequency;

                    long elapsedTimeMillis = currentTimeMillis - lastFrameStart;
                    Time.Delta = elapsedTimeMillis / 1_000.0f;

                    accumulatedTimeMillis += elapsedTimeMillis;

                    long targetFrameTimeMillis = client.TargetFrameTimeMillis;
                    Time.FixedDelta = targetFrameTimeMillis / 1_000.0f;

                    client.BeginFrame();

                    // == Input Step (gather, trigger events)

                    {
                        using var __ = Profiler.Scope("Input Step");

                        client.BeginInputStep();

                        UserInputService.Update();
                        Window.Update();

                        client.EndInputStep();
                    }

                    // == Update Step (process update queue, instruct client to update)

                    {
                        using var __ = Profiler.Scope("Update Queue Processing");

                        while (m_updateQueue.Count > 0)
                        {
                            var item = m_updateQueue.Dequeue();
                            item?.Invoke();
                        }
                    }

                    {
                        using var __ = Profiler.Scope("Update Step");

                        client.BeginUpdateStep();

                        Time.Total = currentTimeMillis / 1_000.0f;

                        {
                            using var ___ = Profiler.Scope("High-Rate Update");
                            client.Update(Time.Delta, Time.Total);
                        }

                        {
                            using var ___ = Profiler.Scope("Fixed-Rate Update");

                            long lastFixedUpdateStart = lastFrameStart;
                            while (accumulatedTimeMillis >= targetFrameTimeMillis)
                            {
                                lastFixedUpdateStart += targetFrameTimeMillis;
                                Time.Total = lastFixedUpdateStart / 1_000.0f;

                                client.FixedUpdate(Time.FixedDelta, Time.Total);
                                accumulatedTimeMillis -= targetFrameTimeMillis;
                            }
                        }

                        Time.Total = currentTimeMillis / 1_000.0f;

                        {
                            using var ___ = Profiler.Scope("Late Update");
                            client.LateUpdate();
                        }

                        client.EndUpdateStep();
                    }

                    // == Render Step (instruct client to render)

                    if (Window.Width > 0 && Window.Height > 0)
                    {
                        using var __ = Profiler.Scope("Render Step");

                        GL.ClearColor(0, 0, 0, 1);
                        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

                        client.BeginRenderStep();

                        client.Render();
                        client.LateRender();

                        client.EndRenderStep();

                        Window.SwapBuffer();
                    }

                    lastFrameStart = currentTimeMillis;

                    if (Profiler.IsEnabled)
                    {
                        Profiler.EndSession();
                        Profiler.IsEnabled = false;
                    }

                    if (elapsedTimeMillis < targetFrameTimeMillis)
                        Thread.Sleep(0);
                }
            }
            finally
            {
                PerformExit(true);
            }
        }

        private void Window_ClientSizeChanged(int width, int height)
        {
        }

        internal void WindowMoved(int x, int y)
        {
            TheoriConfig.Maximized = false;
        }

        internal void WindowMaximized()
        {
            TheoriConfig.Maximized = true;
        }

        internal void WindowMinimized()
        {
        }

        internal void WindowRestored()
        {
        }

        #region Config

        public void LoadConfig()
        {
            using var _ = Profiler.Scope("ClientHost::LoadConfig");
            UserConfigManager.LoadFromFile();
        }

        public void SaveConfig()
        {
            using var _ = Profiler.Scope("ClientHost::SaveConfig");
            UserConfigManager.SaveToFile();
        }

        #endregion

        internal void RequestExit()
        {
            Exit();
        }

        /// <summary>Schedule an exit for the next frame.</summary>
        public void Exit() => PerformExit(false);

        protected virtual void OnActivated() => m_updateQueue.Enqueue(() => Activated?.Invoke());
        protected virtual void OnDeactivated() => m_updateQueue.Enqueue(() => Deactivated?.Invoke());

        private void DoExit()
        {
            //Profiler.EndSession();

            UserConfigManager.SaveToFile(); // TODO(local): Save to the proper file
            Exited?.Invoke();

            Logger.Flush();
            Window.Destroy();

            Environment.Exit(0);
        }

        protected virtual void OnExitRequested()
        {
            // TODO(local): Allow the client to deny an exit request somewhere
            Exit();
        }

        protected virtual void OnExited() => Exited?.Invoke();

        protected internal virtual void PerformExit(bool immediately)
        {
            if (immediately)
                DoExit();
            else m_updateQueue.Enqueue(DoExit);
        }
    }
}
