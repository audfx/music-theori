using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime;
using System.Threading;

using theori.Audio;
using theori.Configuration;
using theori.Graphics;
using theori.Graphics.OpenGL;
using theori.IO;
using theori.Resources;
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

        public static void InitScriptingSystem()
        {
            LuaScript.RegisterType<Anchor>();

            LuaScript.RegisterType<Vector2>();
            LuaScript.RegisterType<Vector3>();
            LuaScript.RegisterType<Vector4>();

            LuaScript.RegisterType<Font>();
            LuaScript.RegisterType<Texture>();
            LuaScript.RegisterType<TextRasterizer>();

            LuaScript.RegisterType<BasicSpriteRenderer>();
            LuaScript.RegisterType<ClientResourceManager>();

            LuaScript.RegisterType<ScriptWindowInterface>();
        }

        public readonly TheoriConfig Config = new TheoriConfig();

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
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            if (File.Exists(GAME_CONFIG_FILE))
                LoadConfig();
            else SaveConfig();

            Window.Create(this);
            Window.VSync = Config.GetEnum<VSyncMode>(TheoriConfigKey.VSync);
            Window.ClientSizeChanged += Window_ClientSizeChanged;

            Mixer.Initialize(new AudioFormat(48000, 2));

            InitScriptingSystem();

            Logger.Log($"Window VSync: { Window.VSync }");
        }

        protected override void DisposeManaged()
        {
            m_stoppedEvent.Wait();
            m_stoppedEvent.Dispose();
        }

        public void Run(Client client)
        {
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
                        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
                        throw (Exception)e.ExceptionObject;
                    } // break;
                }
            }

            try
            {
                AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

                client.SetHost(this);

                var timer = Stopwatch.StartNew();
                long lastFrameStart = timer.ElapsedMilliseconds;

                long accumulatedTime = 0;

                while (true)
                {
                    long currentTime = timer.ElapsedMilliseconds;

                    long elapsedTime = currentTime - lastFrameStart;
                    Time.Delta = elapsedTime / 1_000.0f;

                    accumulatedTime += elapsedTime;

                    long targetFrameTimeMillis = client.TargetFrameTimeMillis;
                    Time.FixedDelta = targetFrameTimeMillis / 1_000.0f;

                    client.BeginFrame();

                    // == Input Step (gather, trigger events)

                    client.BeginInputStep();

                    Keyboard.Update();
                    Mouse.Update();
                    Window.Update();

                    client.EndInputStep();

                    // == Update Step (process update queue, instruct client to update)

                    while (m_updateQueue.Count > 0)
                    {
                        var item = m_updateQueue.Dequeue();
                        item?.Invoke();
                    }

                    client.BeginUpdateStep();

                    Time.Total = currentTime / 1_000.0f;
                    client.Update(Time.Delta, Time.Total);

                    long lastFixedUpdateStart = lastFrameStart;
                    while (accumulatedTime >= targetFrameTimeMillis)
                    {
                        lastFixedUpdateStart += targetFrameTimeMillis;
                        Time.Total = lastFixedUpdateStart / 1_000.0f;

                        client.FixedUpdate(Time.FixedDelta, Time.Total);
                        accumulatedTime -= targetFrameTimeMillis;
                    }

                    Time.Total = currentTime / 1_000.0f;
                    client.LateUpdate();

                    client.EndUpdateStep();

                    // == Render Step (instruct client to render)

                    if (Window.Width > 0 && Window.Height > 0)
                    {
                        GL.ClearColor(0, 0, 0, 1);
                        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

                        client.BeginRenderStep();

                        client.Render();
                        client.LateRender();

                        client.EndRenderStep();

                        Window.SwapBuffer();
                    }

                    lastFrameStart = currentTime;

                    if (elapsedTime < targetFrameTimeMillis)
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
            Config.Set(TheoriConfigKey.Maximized, false);
        }

        internal void WindowMaximized()
        {
            Config.Set(TheoriConfigKey.Maximized, true);
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
            using var reader = new StreamReader(File.OpenRead(GAME_CONFIG_FILE));
            Config.Load(reader);
        }

        public void SaveConfig()
        {
            using var writer = new StreamWriter(File.Open(GAME_CONFIG_FILE, FileMode.Create));
            Config.Save(writer);
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
