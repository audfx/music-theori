using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using theori.Graphics.OpenGL;

using System.Numerics;

using theori.Audio;
using theori.Audio.NVorbis;
using theori.BootLoaders;
using theori.Charting;
using theori.Configuration;
using theori.GameModes;
using theori.Graphics;
using theori.IO;
using theori.Platform;
using theori.Resources;
using theori.Scripting;
using theori.Charting.Effects;

namespace theori
{
    public static class Host
    {
        enum LayerStackOp
        {
            AddBefore,
            AddAfter,
            Remove,
        }

        class LayerStackChange
        {
            public LayerStackOp Operation;

            public Layer Layer;
            public Layer? RelativeTo;

            public bool IsOverlayChange => Layer is Overlay;

            public LayerStackChange(LayerStackOp op, Layer toAdd, Layer? relativeTo = null)
            {
                Operation = op;
                Layer = toAdd;
                RelativeTo = relativeTo;
            }
        }

        private static IPlatform platform;
        public static IPlatform Platform
        {
            get => platform;
            set
            {
                if (platform != null)
                    throw new Exception("Platform already set.");
                platform = value;
            }
        }

        public const string GAME_CONFIG_FILE = "theori-config.ini";
        public static GameConfig GameConfig { get; private set; }

        public static ClientResourceManager StaticResources { get; private set; }

        internal static ProgramPipeline Pipeline { get; private set; }

        private static bool runProgramLoop = false;

        private static readonly List<LayerStackChange> layerStackChanges = new List<LayerStackChange>();

        private static readonly List<Layer> layers = new List<Layer>();
        private static readonly List<Overlay> overlays = new List<Overlay>();

        // TODO(local): Probably make this a dictionary for lookups
        private static readonly List<GameMode> sharedGameModes = new List<GameMode>();

        private static int LayerCount => layers.Count;
        private static int OverlayCount => overlays.Count;

        public static event Action OnInputBegin;
        public static event Action OnInputEnd;

        public static event Action<float, float> OnUpdateBegin;
        public static event Action OnUpdateEnd;

        public static event Action OnRenderBegin;
        public static event Action OnRenderEnd;

        public static event Action OnUserQuit;

        /// <summary>
        /// Adds a new, uninitialized layer to the top of the layer stack.
        /// The layer must never have been in the layer stack before.
        /// 
        /// This is to make sure that the initialization and destruction process
        ///  is well defined, no initialized or destroyed layers are allowed back in.
        /// </summary>
        public static void PushLayer(Layer layer)
        {
            if (layer.lifetimeState != Layer.LayerLifetimeState.Uninitialized)
                throw new Exception("Layer has already been in the layer stack. Cannot re-initialize.");
            layerStackChanges.Add(new LayerStackChange(LayerStackOp.AddAfter, layer, LayerCount == 0 ? null : layers[LayerCount - 1]));
        }

        public static void AddLayerAbove(Layer aboveThis, Layer layer)
        {
            if (layer.lifetimeState != Layer.LayerLifetimeState.Uninitialized)
                throw new Exception("Layer has already been in the layer stack. Cannot re-initialize.");
            layerStackChanges.Add(new LayerStackChange(LayerStackOp.AddAfter, layer, aboveThis));
        }

        public static void AddLayerBelow(Layer belowThis, Layer layer)
        {
            if (layer.lifetimeState != Layer.LayerLifetimeState.Uninitialized)
                throw new Exception("Layer has already been in the layer stack. Cannot re-initialize.");
            layerStackChanges.Add(new LayerStackChange(LayerStackOp.AddBefore, layer, belowThis));
        }

        /// <summary>
        /// Removes the topmost layer from the layer stack and destroys it.
        /// </summary>
        private static void PopLayer()
        {
            layerStackChanges.Add(new LayerStackChange(LayerStackOp.Remove, layers[LayerCount - 1]));
        }

        public static void RemoveLayer(Layer layer)
        {
            layerStackChanges.Add(new LayerStackChange(LayerStackOp.Remove, layer));
        }

        public static void PopToParent(Layer firstChild)
        {
            int childIndex = layers.IndexOf(firstChild);
            if (childIndex < 0) return;

            int numLayersToPop = LayerCount - childIndex;

            for (int i = 0; i < numLayersToPop; i++)
                PopLayer();
        }

        public static void AddOverlay(Overlay overlay)
        {
            if (overlay.lifetimeState != Layer.LayerLifetimeState.Uninitialized)
                throw new Exception("Layer has already been in the layer stack. Cannot re-initialize.");
            if (!overlays.Contains(overlay))
                layerStackChanges.Add(new LayerStackChange(LayerStackOp.AddAfter, overlay, overlays[overlays.Count - 1]));
        }

        public static void RemoveOverlay(Overlay overlay)
        {
            if (overlays.Remove(overlay))
                layerStackChanges.Add(new LayerStackChange(LayerStackOp.Remove, overlay));
        }

        public static void RemoveAllOverlays()
        {
            foreach (var overlay in overlays)
                RemoveOverlay(overlay);
        }

        private static void OnClientSizeChanged(int w, int h)
        {
            layers.ForEach(l => l.ClientSizeChanged(w, h));
            overlays.ForEach(l => l.ClientSizeChanged(w, h));
        }

        #region Initialization

        public static void DefaultInitialize()
        {
            if (platform == null)
                throw new Exception("Cannot initialize :theori without a platform implementation.");

            InitGameConfig();
            InitWindowSystem();
            Logger.Log($"Window VSync: { Window.VSync }");
            InitGraphicsPipeline();
            InitAudioSystem();
            InitScriptingSystem();
            InitClientResources();
        }

        public static bool InitGameConfig()
        {
            GameConfig = new GameConfig();
            if (File.Exists(GAME_CONFIG_FILE))
                LoadConfig();
            // save the defaults on init
            else SaveConfig();
            return File.Exists(GAME_CONFIG_FILE);
        }

        public static bool InitWindowSystem()
        {
            Window.Create();
            Window.VSync = VSyncMode.Off;

            Window.ClientSizeChanged += OnClientSizeChanged;

            return true;
        }

        public static bool InitGraphicsPipeline()
        {
            Pipeline = new ProgramPipeline();
            Pipeline.Bind();
            return true;
        }

        public static bool InitAudioSystem()
        {
            //CodecFactory.Instance.Register("ogg-vorbis", new CodecFactoryEntry(s => new NVorbisSource(s).ToWaveSource(), ".ogg"));

            Mixer.Initialize(new AudioFormat(48000, 2));

            //Mixer = new Mixer(2);
            //Mixer.MasterChannel.Volume = GameConfig.GetFloat(GameConfigKey.MasterVolume);

            return true;
        }

        public static bool InitScriptingSystem()
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

            return true;
        }

        public static bool InitClientResources()
        {
            StaticResources = new ClientResourceManager(ClientResourceLocator.Default);
            return true;
        }

        #endregion

        #region Config

        public static void LoadConfig()
        {
            using (var reader = new StreamReader(File.OpenRead(GAME_CONFIG_FILE)))
                GameConfig.Load(reader);
        }

        public static void SaveConfig()
        {
            using (var writer = new StreamWriter(File.Open(GAME_CONFIG_FILE, FileMode.Create)))
                GameConfig.Save(writer);
        }

        #endregion

        public static void RegisterSharedGameMode(GameMode gameMode)
        {
            if (!gameMode.SupportsSharedUsage)
            {
                Logger.Log($"{ nameof(RegisterSharedGameMode) } called with a game mode that does not support shared usage.");
                return;
            }

            foreach (var mode in sharedGameModes)
            {
                // simply don't add exact duplicates
                if (mode == gameMode) return;
                if (mode.Name == gameMode.Name)
                {
                    Logger.Log("Attempt to add a game mode with the same name as a previously added game mode. Until unique identification is added, this is illegal.");
                    return;
                }
            }

            sharedGameModes.Add(gameMode);
            Entity.RegisterTypesFromGameMode(gameMode);
            EffectDef.RegisterTypesFromGameMode(gameMode);
        }

        private static bool ProcessLayerStackChanges()
        {
            bool processed = layerStackChanges.Count > 0;

            foreach (var change in layerStackChanges)
            {
                bool isOverlay = change.IsOverlayChange;

                switch (change.Operation)
                {
                    case LayerStackOp.AddAfter:
                    {
                        var aboveThis = change.RelativeTo!;
                        var layer = change.Layer;

                        if (isOverlay)
                        {
                            if (aboveThis == null)
                                overlays.Add((Overlay)layer);
                            else overlays.Insert(overlays.IndexOf((Overlay)aboveThis) + 1, (Overlay)layer);
                        }
                        else
                        {
                            if (aboveThis == null)
                                layers.Add(layer);
                            else
                            {
                                if (!layers.Contains(aboveThis))
                                    throw new Exception("Cannot add a layer above one which is not in the layer stack.");

                                int index = layers.IndexOf(aboveThis);
                                layers.Insert(index + 1, layer);

                                if (layer.BlocksParentLayer)
                                {
                                    for (int i = index; i >= 0; i--)
                                    {
                                        var nextLayer = layers[i];
                                        nextLayer.Suspend();

                                        if (nextLayer.BlocksParentLayer)
                                        {
                                            // if it blocks the previous layers then this has happened already for higher layers.
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        layer.Init();
                        layer.lifetimeState = Layer.LayerLifetimeState.Alive;
                    } break;

                    case LayerStackOp.AddBefore:
                    {
                        var belowThis = change.RelativeTo!;
                        var layer = change.Layer;

                        if (isOverlay)
                            overlays.Insert(overlays.IndexOf((Overlay)belowThis), (Overlay)layer);
                        else
                        {
                            if (!layers.Contains(belowThis))
                                throw new Exception("Cannot add a layer above one which is not in the layer stack.");

                            int index = layers.IndexOf(belowThis);
                            layers.Insert(index, layer);

                            if (layer.BlocksParentLayer)
                            {
                                for (int i = index - 1; i >= 0; i--)
                                {
                                    var nextLayer = layers[i];
                                    nextLayer.Suspend();

                                    if (nextLayer.BlocksParentLayer)
                                    {
                                        // if it blocks the previous layers then this has happened already for higher layers.
                                        break;
                                    }
                                }
                            }

                            if (belowThis.BlocksParentLayer)
                            {
                                for (int i = index; i >= 0; i--)
                                {
                                    var nextLayer = layers[i];
                                    nextLayer.Suspend();

                                    if (nextLayer.BlocksParentLayer)
                                    {
                                        // if it blocks the previous layers then this has happened already for higher layers.
                                        break;
                                    }
                                }
                            }
                        }

                        layer.Init();
                        layer.lifetimeState = Layer.LayerLifetimeState.Alive;
                    } break;

                    case LayerStackOp.Remove:
                    {
                        var layer = change.Layer;

                        if (layer.lifetimeState != Layer.LayerLifetimeState.Alive)
                            throw new ArgumentException("Layer to remove was not alive.");

                        layer.DestroyInternal();
                        layer.lifetimeState = Layer.LayerLifetimeState.Destroyed;

                        if (isOverlay)
                            overlays.Remove((Overlay)layer);
                        else
                        {
                            int index = layers.IndexOf(layer);
                            layers.RemoveAt(index);

                            if (!layer.IsSuspended)
                            {
                                if (layer.BlocksParentLayer)
                                {
                                    int startIndex = index - 1;
                                    for (; startIndex >= 0; startIndex--)
                                    {
                                        if (layers[startIndex].BlocksParentLayer)
                                        {
                                            // if it blocks the previous layers then this will happen later for higher layers.
                                            break;
                                        }
                                    }

                                    // resume layers bottom to top
                                    for (int i = startIndex; i < LayerCount; i++)
                                        layers[i].Resume();
                                }
                            }
                        }
                    } break;
                }
            }

            layerStackChanges.Clear();
            if (LayerCount == 0)
                runProgramLoop = false;

            return processed;
        }

        private static void ProgramLoop()
        {
            var timer = Stopwatch.StartNew();
            long lastFrameStart = timer.ElapsedMilliseconds;

            runProgramLoop = true;
            while (runProgramLoop && (LayerCount > 0 || layerStackChanges.Count > 0) && !Window.ShouldExitApplication)
            {
                if (LayerCount == 0)
                    ProcessLayerStackChanges();

                int targetFrameRate, layerStartIndex;
                long targetFrameTimeMillis;

                void CalcLayerStackStuff()
                {
                    targetFrameRate = 0;
                    layerStartIndex = LayerCount - 1;

                    for (; layerStartIndex >= 0; layerStartIndex--)
                    {
                        var layer = layers[layerStartIndex];
                        targetFrameRate = MathL.Max(targetFrameRate, layer.TargetFrameRate);

                        if (layer.BlocksParentLayer)
                            break;
                    }

                    layerStartIndex = Math.Max(0, layerStartIndex);

                    if (targetFrameRate == 0)
                        targetFrameRate = 60; // TODO(local): configurable target frame rate plz

                    targetFrameTimeMillis = 1_000 / targetFrameRate;
                }

                CalcLayerStackStuff();

                long currentTime = timer.ElapsedMilliseconds;
                long elapsedTime = currentTime - lastFrameStart;

                bool updated = false;
                while (elapsedTime > targetFrameTimeMillis)
                {
                    updated = true;
                    lastFrameStart = currentTime;

                    elapsedTime -= targetFrameTimeMillis;

                    Time.Delta = targetFrameTimeMillis / 1_000.0f;
                    Time.Total = lastFrameStart / 1_000.0f;

                    if (ProcessLayerStackChanges())
                        CalcLayerStackStuff();

                    OnInputBegin?.Invoke();

                    Keyboard.Update();
                    Mouse.Update();
                    Window.Update();

                    OnInputEnd?.Invoke();

                    if (Window.ShouldExitApplication)
                    {
                        Quit();
                        return;
                    }

                    OnUpdateBegin?.Invoke(Time.Delta, Time.Total);

                    // update top down
                    for (int i = OverlayCount - 1; i >= 0 && runProgramLoop; i--)
                        overlays[i].UpdateInternal(Time.Delta, Time.Total);

                    for (int i = LayerCount - 1; i >= layerStartIndex && runProgramLoop; i--)
                        layers[i].UpdateInternal(Time.Delta, Time.Total);

                    OnUpdateEnd?.Invoke();
                }

                if (!runProgramLoop) break;
                if (updated && Window.Width > 0 && Window.Height > 0)
                {
                    OnRenderBegin?.Invoke();

                    GL.ClearColor(0, 0, 0, 1);
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

                    // render bottom up
                    for (int i = layerStartIndex; i < LayerCount; i++)
                        layers[i].RenderInternal();

                    for (int i = 0; i < OverlayCount; i++)
                        overlays[i].RenderInternal();

                    OnRenderEnd?.Invoke();

                    Window.SwapBuffer();
                }
            }

            Quit();
        }

        /// <summary>
        /// Returns the host to a post-initialized state so Start* functions can be called again cleanly.
        /// </summary>
        public static void Restart()
        {
            runProgramLoop = false;
        }

        public static void StartStandalone(GameMode gameMode, string[] args)
        {
            if (gameMode != null)
            {
                Entity.RegisterTypesFromGameMode(gameMode);
                EffectDef.RegisterTypesFromGameMode(gameMode);
                gameMode.InvokeStandalone(args);
            }
            else PushLayer(new StandaloneBootLoader(args));

            ProgramLoop();
        }

        public static void StartShared(string[] args)
        {
            PushLayer(new SharedBootLoader(args));
            ProgramLoop();
        }

        public static void Quit(int code = 0)
        {
            OnUserQuit?.Invoke();

            SaveConfig();

            StaticResources.Dispose();

            //Gamepad.Destroy();
            Window.Destroy();

            Environment.Exit(code);
        }

        internal static void WindowMoved(int x, int y)
        {
            GameConfig.Set(GameConfigKey.Maximized, false);
        }

        internal static void WindowMaximized()
        {
            GameConfig.Set(GameConfigKey.Maximized, true);
        }

        internal static void WindowMinimized()
        {
        }

        internal static void WindowRestored()
        {
        }

        internal static void KeyPressed(KeyInfo info)
        {
            for (int i = OverlayCount - 1; i >= 0; i--)
            {
                var overlay = overlays[i];
                if (overlay.KeyPressed(info))
                    break;
            }

            for (int i = LayerCount - 1; i >= 0; i--)
            {
                var layer = layers[i];
                if (layer.KeyPressed(info) || layer.BlocksParentLayer)
                    break;
            }
        }

        internal static void KeyReleased(KeyInfo info)
        {
            for (int i = OverlayCount - 1; i >= 0; i--)
            {
                var overlay = overlays[i];
                if (overlay.KeyReleased(info))
                    break;
            }

            for (int i = LayerCount - 1; i >= 0; i--)
            {
                var layer = layers[i];
                if (layer.KeyReleased(info) || layer.BlocksParentLayer)
                    break;
            }
        }

        internal static void ButtonPressed(ButtonInfo info)
        {
            for (int i = OverlayCount - 1; i >= 0; i--)
            {
                var overlay = overlays[i];
                if (overlay.ButtonPressed(info))
                    break;
            }

            for (int i = LayerCount - 1; i >= 0; i--)
            {
                var layer = layers[i];
                if (layer.ButtonPressed(info) || layer.BlocksParentLayer)
                    break;
            }
        }

        internal static void ButtonReleased(ButtonInfo info)
        {
            for (int i = OverlayCount - 1; i >= 0; i--)
            {
                var overlay = overlays[i];
                if (overlay.ButtonReleased(info))
                    break;
            }

            for (int i = LayerCount - 1; i >= 0; i--)
            {
                var layer = layers[i];
                if (layer.ButtonReleased(info) || layer.BlocksParentLayer)
                    break;
            }
        }

        internal static void AxisChanged(AnalogInfo info)
        {
            for (int i = OverlayCount - 1; i >= 0; i--)
            {
                var overlay = overlays[i];
                if (overlay.AxisChanged(info))
                    break;
            }

            for (int i = LayerCount - 1; i >= 0; i--)
            {
                var layer = layers[i];
                if (layer.AxisChanged(info) || layer.BlocksParentLayer)
                    break;
            }
        }
    }
}
