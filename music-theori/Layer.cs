using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using theori.Audio;
using theori.Charting;
using theori.Charting.Serialization;
using theori.Configuration;
using theori.GameModes;
using theori.Graphics;
using theori.Graphics.OpenGL;
using theori.IO;
using theori.Platform;
using theori.Resources;
using theori.Scripting;

using static MoonSharp.Interpreter.DynValue;

namespace theori
{
    public class Layer : LuaInstance, IAsyncLoadable
    {
        static Layer()
        {
            var desc = (StandardUserDataDescriptor)ScriptService.RegisterType<Layer>();

            foreach (var field in typeof(Layer).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!(field.Name.StartsWith("Lua") && field.Name.EndsWith("Event")))
                    continue;

                var fieldDesc = new FieldMemberDescriptor(field, InteropAccessMode.Preoptimized);
                desc!.AddMember(field.Name.Substring(3, field.Name.Length - 8), fieldDesc);
            }
        }

        #region Lifetime

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

        [MoonSharpHidden]
        public bool IsSuspended { get; private set; } = false;

        internal bool validForResume = true;

        #endregion

        #region Platform

        private Client? m_client = null;
        [MoonSharpHidden]
        public Client Client => m_client ?? throw new InvalidOperationException("Layer has not been initialized with a client yet.");

        [MoonSharpHidden]
        public T ClientAs<T>() where T : Client => (T)Client;

        [MoonSharpHidden]
        public ClientHost Host => Client.Host;

        [MoonSharpHidden]
        public T HostAs<T>() where T : ClientHost => (T)Host;

        internal void SetClient(Client client)
        {
            if (m_client != null) throw new InvalidOperationException("Layer already has a client assigned to it.");
            m_client = client;
        }

        #endregion

        #region Layer Configuration

        [MoonSharpHidden]
        public virtual int TargetFrameRate => 0;

        [MoonSharpHidden]
        public virtual bool BlocksParentLayer => true;

        #endregion

        #region Resource Management

        [MoonSharpHidden]
        public readonly ClientResourceLocator ResourceLocator;
        [MoonSharpHidden]
        protected readonly ClientResourceManager m_resources;

        #endregion

        #region Scripting Interface

        [MoonSharpHidden]
        public readonly ExecutionEnvironment ScriptExecutionEnvironment;

#if false
        private readonly BasicSpriteRenderer m_spriteRenderer;
        private readonly RenderBatch2D m_renderer2D;
        private RenderBatcher2D? m_batch = null;

        protected readonly ScriptProgram m_script;
#endif

        private string? m_drivingScriptFileName = null;
        private DynValue[]? m_drivingScriptArgs = null;

#if false
        [MoonSharpHidden]
        public readonly Table tblTheori;
        [MoonSharpHidden]
        public readonly Table tblTheoriAudio;
        [MoonSharpHidden]
        public readonly Table tblTheoriCharts;
        [MoonSharpHidden]
        public readonly Table tblTheoriConfig;
        [MoonSharpHidden]
        public readonly Table tblTheoriGame;
        [MoonSharpHidden]
        public readonly Table tblTheoriGraphics;
        [MoonSharpHidden]
        public readonly Table tblTheoriInput;
        [MoonSharpHidden]
        public readonly Table tblTheoriLayer;
        [MoonSharpHidden]
        public readonly Table tblTheoriModes;
        [MoonSharpHidden]
        public readonly Table tblTheoriInputKeyboard;
        [MoonSharpHidden]
        public readonly Table tblTheoriInputMouse;
        [MoonSharpHidden]
        public readonly Table tblTheoriInputGamepad;
        [MoonSharpHidden]
        public readonly Table tblTheoriInputController;
#endif

#endregion

        protected ClientResourceManager StaticResources => theori.Host.StaticResources;

        private readonly LuaBindableEvent LuaAsyncLoadEvent;
        private readonly LuaBindableEvent LuaAsyncFinalizeEvent;

        private readonly LuaBindableEvent LuaInitializeEvent;
        private readonly LuaBindableEvent LuaDestroyEvent;
        private readonly LuaBindableEvent LuaSuspendEvent;
        private readonly LuaBindableEvent LuaResumeEvent;
        private readonly LuaBindableEvent LuaExitingEvent;

        private readonly LuaBindableEvent LuaKeyPressEvent;
        private readonly LuaBindableEvent LuaKeyReleaseEvent;

        private readonly LuaBindableEvent LuaMousePressEvent;
        private readonly LuaBindableEvent LuaMouseReleaseEvent;
        private readonly LuaBindableEvent LuaMouseMoveEvent;
        private readonly LuaBindableEvent LuaMouseScrollEvent;

        private readonly LuaBindableEvent LuaGamepadConnectEvent;
        private readonly LuaBindableEvent LuaGamepadDisconnectEvent;
        private readonly LuaBindableEvent LuaGamepadPressEvent;
        private readonly LuaBindableEvent LuaGamepadReleaseEvent;
        private readonly LuaBindableEvent LuaGamepadAxisChangeEvent;

        private readonly LuaBindableEvent LuaControllerAddEvent;
        private readonly LuaBindableEvent LuaControllerRemoveEvent;
        private readonly LuaBindableEvent LuaControllerPressEvent;
        private readonly LuaBindableEvent LuaControllerReleaseEvent;
        private readonly LuaBindableEvent LuaControllerAxisChangeEvent;
        private readonly LuaBindableEvent LuaControllerAxisTickEvent;

        private readonly LuaBindableEvent LuaUpdateEvent;
        private readonly LuaBindableEvent LuaRenderEvent;

        public Layer(ExecutionEnvironment env, ClientResourceLocator? resourceLocator = null, string? layerPathLua = null, params DynValue[] args)
            : base(env)
        {
            ScriptExecutionEnvironment = env;

            ResourceLocator = resourceLocator ?? ClientResourceLocator.Default;

            m_resources = new ClientResourceManager(ResourceLocator);

            LuaAsyncLoadEvent = new LuaBindableEvent(L);
            LuaAsyncFinalizeEvent = new LuaBindableEvent(L);

            LuaInitializeEvent = new LuaBindableEvent(L);
            LuaDestroyEvent = new LuaBindableEvent(L);
            LuaSuspendEvent = new LuaBindableEvent(L);
            LuaResumeEvent = new LuaBindableEvent(L);
            LuaExitingEvent = new LuaBindableEvent(L);
            
            LuaKeyPressEvent = new LuaBindableEvent(L);
            LuaKeyReleaseEvent = new LuaBindableEvent(L);
            
            LuaMousePressEvent = new LuaBindableEvent(L);
            LuaMouseReleaseEvent = new LuaBindableEvent(L);
            LuaMouseMoveEvent = new LuaBindableEvent(L);
            LuaMouseScrollEvent = new LuaBindableEvent(L);
            
            LuaGamepadConnectEvent = new LuaBindableEvent(L);
            LuaGamepadDisconnectEvent = new LuaBindableEvent(L);
            LuaGamepadPressEvent = new LuaBindableEvent(L);
            LuaGamepadReleaseEvent = new LuaBindableEvent(L);
            LuaGamepadAxisChangeEvent = new LuaBindableEvent(L);
            
            LuaControllerAddEvent = new LuaBindableEvent(L);
            LuaControllerRemoveEvent = new LuaBindableEvent(L);
            LuaControllerPressEvent = new LuaBindableEvent(L);
            LuaControllerReleaseEvent = new LuaBindableEvent(L);
            LuaControllerAxisChangeEvent = new LuaBindableEvent(L);
            LuaControllerAxisTickEvent = new LuaBindableEvent(L);

            LuaUpdateEvent = new LuaBindableEvent(L);
            LuaRenderEvent = new LuaBindableEvent(L);

#if false
            m_script = new ScriptProgram(ResourceLocator);

            m_spriteRenderer = new BasicSpriteRenderer(ResourceLocator);
            m_renderer2D = new RenderBatch2D(m_resources);
#endif

            m_drivingScriptFileName = layerPathLua;
            m_drivingScriptArgs = args;

#if false
            m_script["KeyCode"] = typeof(KeyCode);
            m_script["MouseButton"] = typeof(MouseButton);

            m_script["theori"] = tblTheori = m_script.NewTable();

            tblTheori["audio"] = tblTheoriAudio = m_script.NewTable();
            tblTheori["charts"] = tblTheoriCharts = m_script.NewTable();
            tblTheori["config"] = tblTheoriConfig = m_script.NewTable();
            tblTheori["game"] = tblTheoriGame = m_script.NewTable();
            tblTheori["graphics"] = tblTheoriGraphics = m_script.NewTable();
            tblTheori["input"] = tblTheoriInput = m_script.NewTable();
            tblTheori["layer"] = tblTheoriLayer = m_script.NewTable();
            tblTheori["modes"] = tblTheoriModes = m_script.NewTable();

            tblTheoriInput["keyboard"] = tblTheoriInputKeyboard = m_script.NewTable();
            tblTheoriInput["mouse"] = tblTheoriInputMouse = m_script.NewTable();
            tblTheoriInput["gamepad"] = tblTheoriInputGamepad = m_script.NewTable();
            tblTheoriInput["controller"] = tblTheoriInputController = m_script.NewTable();

            tblTheoriInputKeyboard["pressed"] = evtKeyPressed = m_script.NewEvent();
            tblTheoriInputKeyboard["released"] = evtKeyReleased = m_script.NewEvent();

            tblTheoriInputMouse["pressed"] = evtMousePressed = m_script.NewEvent();
            tblTheoriInputMouse["released"] = evtMouseReleased = m_script.NewEvent();
            tblTheoriInputMouse["moved"] = evtMouseMoved = m_script.NewEvent();
            tblTheoriInputMouse["scrolled"] = evtMouseScrolled = m_script.NewEvent();
            tblTheoriInputMouse["getMousePosition"] = (Func<DynValue>)(() => NewTuple(NewNumber(UserInputService.MouseX), NewNumber(UserInputService.MouseY)));

            tblTheoriInputGamepad["connected"] = evtGamepadConnected = m_script.NewEvent();
            tblTheoriInputGamepad["disconnected"] = evtGamepadDisconnected = m_script.NewEvent();
            tblTheoriInputGamepad["pressed"] = evtGamepadPressed = m_script.NewEvent();
            tblTheoriInputGamepad["released"] = evtGamepadReleased = m_script.NewEvent();
            tblTheoriInputGamepad["axisChanged"] = evtGamepadAxisChanged = m_script.NewEvent();

            tblTheoriInputController["added"] = evtControllerAdded = m_script.NewEvent();
            tblTheoriInputController["removed"] = evtControllerRemoved = m_script.NewEvent();
            tblTheoriInputController["pressed"] = evtControllerPressed = m_script.NewEvent();
            tblTheoriInputController["released"] = evtControllerReleased = m_script.NewEvent();
            tblTheoriInputController["axisChanged"] = evtControllerAxisChanged = m_script.NewEvent();
            tblTheoriInputController["axisTicked"] = evtControllerAxisTicked = m_script.NewEvent();

            tblTheori["doStaticLoadsAsync"] = (Func<bool>)(() => StaticResources.LoadAll());
            tblTheori["finalizeStaticLoads"] = (Func<bool>)(() => StaticResources.FinalizeLoad());

            tblTheoriAudio["queueStaticAudioLoad"] = (Func<string, AudioHandle>)(audioName => new AudioHandle(m_script, StaticResources.QueueAudioLoad($"audio/{ audioName }")));
            tblTheoriAudio["getStaticAudio"] = (Func<string, AudioHandle>)(audioName => new AudioHandle(m_script, StaticResources.GetAudio($"audio/{ audioName }")));
            tblTheoriAudio["queueAudioLoad"] = (Func<string, AudioHandle>)(audioName => new AudioHandle(m_script, m_resources.QueueAudioLoad($"audio/{ audioName }")));
            tblTheoriAudio["getAudio"] = (Func<string, AudioHandle>)(audioName => new AudioHandle(m_script, m_resources.GetAudio($"audio/{ audioName }")));
            tblTheoriAudio["createFakeAudio"] = (Func<int, int, AudioHandle>)((sampleRate, channels) => new AudioHandle(m_script, new FakeAudioSource(sampleRate, channels)));
            
            tblTheoriCharts["setDatabaseToIdle"] = (Action)(() => Client.DatabaseWorker.SetToIdle());
            tblTheoriCharts["getDatabaseState"] = (Func<string>)(() => Client.DatabaseWorker.State.ToString());

            tblTheoriCharts["setDatabaseToClean"] = (Action<DynValue>)(arg =>
            {
                Client.DatabaseWorker.SetToClean(arg == DynValue.Void ? (Action?)null : () => m_script.Call(arg));
            });

            tblTheoriCharts["setDatabaseToPopulate"] = NewCallback((ctx, args) =>
            {
                Action? callback = args.Count == 0 ? (Action?)null : () => ctx.Call(args[0]);
                Client.DatabaseWorker.SetToPopulate(callback);
                return Nil;
            });
            
            tblTheoriCharts["create"] = (Func<string, ChartHandle>)(modeName =>
            {
                var mode = GameMode.GetInstance(modeName);
                //if (mode == null) return DynValue.Nil;
                return new ChartHandle(m_resources, m_script, Client.DatabaseWorker, mode!.GetChartFactory().CreateNew());
            });
            tblTheoriCharts["newEntity"] = (Func<string, Entity>)(entityTypeId =>
            {
                var entityType = Entity.GetEntityTypeById(entityTypeId);
                return (Entity)Activator.CreateInstance(entityType!);
            });
            tblTheoriCharts["saveChartToDatabase"] = (Action<Chart>)(chart =>
            {
                var ser = chart.GameMode.CreateChartSerializer(TheoriConfig.ChartsDirectory, chart.Info.ChartFileType) ?? new TheoriChartSerializer(TheoriConfig.ChartsDirectory, chart.GameMode);
                var setSer = new ChartSetSerializer(TheoriConfig.ChartsDirectory);

                setSer.SaveToFile(chart.SetInfo);
                ser.SaveToFile(chart);
            });

            tblTheoriCharts["createCollection"] = (Action<string>)(collectionName => Client.DatabaseWorker.CreateCollection(collectionName));
            tblTheoriCharts["addChartToCollection"] = (Action<string, ChartInfoHandle>)((collectionName, chart) => Client.DatabaseWorker.AddChartToCollection(collectionName, chart));
            tblTheoriCharts["removeChartFromCollection"] = (Action<string, ChartInfoHandle>)((collectionName, chart) => Client.DatabaseWorker.RemoveChartFromCollection(collectionName, chart));
            tblTheoriCharts["getCollectionNames"] = (Func<string[]>)(() => Client.DatabaseWorker.CollectionNames);

            tblTheoriCharts["getChartSets"] = (Func<List<ChartSetInfoHandle>>)(() => Client.DatabaseWorker.ChartSets.Select(info => new ChartSetInfoHandle(m_resources, m_script, Client.DatabaseWorker, info)).ToList());
            tblTheoriCharts["getChartSetsFiltered"] = (Func<string?, DynValue, DynValue, DynValue, List<List<ChartInfoHandle>>>)((col, a, b, c) =>
            {
                Logger.Log("Attempting to filter charts...");

                var setInfoHandles = new Dictionary<ChartSetInfo, ChartSetInfoHandle>();
                ChartSetInfoHandle GetSetInfoHandle(ChartSetInfo chartSet)
                {
                    if (!setInfoHandles.TryGetValue(chartSet, out var result))
                        result = setInfoHandles[chartSet] = new ChartSetInfoHandle(m_resources, m_script, Client.DatabaseWorker, chartSet);
                    return result;
                }

                Logger.Log(col ?? "null");
                var charts = col != null ? Client.DatabaseWorker.GetChartsInCollection(col) : Client.DatabaseWorker.Charts;
                var filteredCharts = from initialChart in charts
                                     let handle = new ChartInfoHandle(GetSetInfoHandle(initialChart.Set), initialChart)
                                     where m_script.Call(a, handle).CastToBool()
                                     select handle;

                var groupedCharts = filteredCharts.OrderBy(chart => m_script.Call(b, chart), DynValueComparer.Instance)
                              .GroupBy(chart => m_script.Call(b, chart))
                              .Select(theGroup => (theGroup.Key, Value: theGroup
                                  .OrderBy(chart => chart.DifficultyIndex)
                                  .ThenBy(chart => chart.DifficultyName)
                                  .ThenBy(chart => m_script.Call(c, chart), DynValueComparer.Instance)
                                  .Select(chart => chart)
                                  .ToList()))
                              .OrderBy(l => l.Key, DynValueComparer.Instance)
                              .Select(l => l.Value)
                              .ToList();

                return groupedCharts;
            });

            tblTheoriConfig["get"] = (Func<string, DynValue>)(key => FromObject(m_script.Script, UserConfigManager.GetFromKey(key)));
            tblTheoriConfig["set"] = (Action<string, DynValue>)((key, value) => UserConfigManager.SetFromKey(key, value.ToObject()));

            tblTheoriGame["exit"] = (Action)(() => Host.Exit());

            tblTheoriGraphics["queueStaticTextureLoad"] = (Func<string, Texture>)(textureName => StaticResources.QueueTextureLoad($"textures/{ textureName }"));
            tblTheoriGraphics["getStaticTexture"] = (Func<string, Texture>)(textureName => StaticResources.GetTexture($"textures/{ textureName }"));
            tblTheoriGraphics["queueTextureLoad"] = (Func<string, Texture>)(textureName => m_resources.QueueTextureLoad($"textures/{ textureName }"));
            tblTheoriGraphics["getTexture"] = (Func<string, Texture>)(textureName => m_resources.GetTexture($"textures/{ textureName }"));
            tblTheoriGraphics["createFont"] = (Func<string, VectorFont>)(fontName => new VectorFont(ResourceLocator.OpenFileStreamWithExtension($"fonts/{ fontName }", new[] { ".ttf", ".otf" }, out string _)));
            //tblTheoriGraphics["getFont"] = (Func<string, VectorFont>)(fontName => m_resources.GetTexture($"fonts/{ fontName }"));
            tblTheoriGraphics["getViewportSize"] = (Func<DynValue>)(() => NewTuple(NewNumber(Window.Width), NewNumber(Window.Height)));
            tblTheoriGraphics["createPathCommands"] = (Func<Path2DCommands>)(() => new Path2DCommands());
            tblTheoriGraphics["flush"] = (Action)(() => m_batch?.Flush());
            tblTheoriGraphics["saveTransform"] = (Action)(() => m_batch?.SaveTransform());
            tblTheoriGraphics["restoreTransform"] = (Action)(() => m_batch?.RestoreTransform());
            tblTheoriGraphics["resetTransform"] = (Action)(() => m_batch?.ResetTransform());
            tblTheoriGraphics["translate"] = (Action<float, float>)((x, y) => m_batch?.Translate(x, y));
            tblTheoriGraphics["rotate"] = (Action<float>)(d => m_batch?.Rotate(d));
            tblTheoriGraphics["scale"] = (Action<float, float>)((x, y) => m_batch?.Scale(x, y));
            tblTheoriGraphics["shear"] = (Action<float, float>)((x, y) => m_batch?.Shear(x, y));
            tblTheoriGraphics["setFillToColor"] = (Action<float, float, float, float>)((r, g, b, a) => m_batch?.SetFillColor(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f));
            tblTheoriGraphics["setFillToTexture"] = (Action<Texture, float, float, float, float>)((texture, r, g, b, a) => m_batch?.SetFillTexture(texture, new Vector4(r, g, b, a) / 255.0f));
            tblTheoriGraphics["fillRect"] = (Action<float, float, float, float>)((x, y, w, h) => m_batch?.FillRectangle(x, y, w, h));
            tblTheoriGraphics["fillRoundedRect"] = (Action<float, float, float, float, float>)((x, y, w, h, r) => m_batch?.FillRoundedRectangle(x, y, w, h, r));
            tblTheoriGraphics["fillRoundedRectVarying"] = (Action<float, float, float, float, float, float, float, float>)((x, y, w, h, rtl, rtr, rbr, rbl) => m_batch?.FillRoundedRectangleVarying(x, y, w, h, rtl, rtr, rbr, rbl));
            tblTheoriGraphics["setFont"] = (Action<VectorFont?>)(font => m_batch?.SetFont(font));
            tblTheoriGraphics["setFontSize"] = (Action<int>)(size => m_batch?.SetFontSize(size));
            tblTheoriGraphics["setTextAlign"] = (Action<Anchor>)(align => m_batch?.SetTextAlign(align));
            tblTheoriGraphics["fillString"] = (Action<string, float, float>)((text, x, y) => m_batch?.FillString(text, x, y));
            tblTheoriGraphics["fillPathAt"] = (Action<Path2DCommands, float, float, float, float>)((path, x, y, sx, sy) => m_batch?.FillPathAt(path, x, y, sx, sy));
            tblTheoriGraphics["saveScissor"] = (Action)(() => m_batch?.SaveScissor());
            tblTheoriGraphics["restoreScissor"] = (Action)(() => m_batch?.RestoreScissor());
            tblTheoriGraphics["resetScissor"] = (Action)(() => m_batch?.ResetScissor());
            tblTheoriGraphics["scissor"] = (Action<float, float, float, float>)((x, y, w, h) => m_batch?.Scissor(x, y, w, h));
            // setColor -> setFillToColor
            // setImageColor -> setFillToTexture
            // draw -> fillRect
            // drawString -> fillString

            tblTheoriGraphics["openCurtain"] = (Action)OpenCurtain;
            tblTheoriGraphics["closeCurtain"] = (Action<float, DynValue?>)((duration, callback) =>
            {
                Action? onClosed = (callback == null || callback == Nil) ? (Action?)null : () => m_script.Call(callback!);
                if (duration <= 0)
                    CloseCurtain(onClosed);
                else CloseCurtain(duration, onClosed);
            });

            static UserInputService.Modes GetModeFromString(string inputMode) => inputMode switch
            {
                "desktop" => UserInputService.Modes.Desktop,
                "gamepad" => UserInputService.Modes.Gamepad,
                "controller" => UserInputService.Modes.Controller,
                _ => 0,
            };

            tblTheoriInput["setInputModes"] = NewCallback((ctx, args) =>
            {
                UserInputService.Modes modes = UserInputService.Modes.None;
                for (int i = 0; i < args.Count; i++)
                {
                    string arg = args[i].CheckType("setInputModes", MoonSharp.Interpreter.DataType.String).String;
                    modes |= GetModeFromString(arg);
                }
                UserInputService.SetInputMode(modes);
                return Nil;
            });
            tblTheoriInput["isInputModeEnabled"] = (Func<string, bool>)(modeName => UserInputService.InputModes.HasFlag(GetModeFromString(modeName)));

            tblTheoriLayer["construct"] = (Action)(() => { });
            tblTheoriLayer["push"] = DynValue.NewCallback((context, args) =>
            {
                if (args.Count == 0) return Nil;

                string layerPath = args.AsStringUsingMeta(context, 0, "push");
                DynValue[] rest = args.GetArray(1);

                Push(CreateNewLuaLayer(layerPath, rest));
                return Nil;
            });
            tblTheoriLayer["pop"] = (Action)(() => Pop());
            tblTheoriLayer["setInvalidForResume"] = (Action)(() => SetInvalidForResume());
            tblTheoriLayer["doAsyncLoad"] = (Func<bool>)(() => true);
            tblTheoriLayer["doAsyncFinalize"] = (Func<bool>)(() => true);
            tblTheoriLayer["init"] = (Action)(() => { });
            tblTheoriLayer["destroy"] = (Action)(() => { });
            tblTheoriLayer["suspended"] = (Action)(() => { });
            tblTheoriLayer["resumed"] = (Action)(() => { });
            tblTheoriLayer["onExiting"] = (Action)(() => { });
            tblTheoriLayer["onClientSizeChanged"] = (Action<int, int, int, int>)((x, y, w, h) => { });
            tblTheoriLayer["update"] = (Action<float, float>)((delta, total) => { });
            tblTheoriLayer["render"] = (Action)(() => { });
#endif
        }

        class DynValueComparer : IComparer<DynValue>
        {
            public static readonly DynValueComparer Instance = new DynValueComparer();

            private DynValueComparer()
            {
            }

            int IComparer<DynValue>.Compare(DynValue x, DynValue y)
            {
                if (x.Type == MoonSharp.Interpreter.DataType.String &&
                    y.Type == MoonSharp.Interpreter.DataType.String)
                {
                    return string.Compare(x.String, y.String, true);
                }
                else if (x.Type == MoonSharp.Interpreter.DataType.Number &&
                         y.Type == MoonSharp.Interpreter.DataType.Number)
                {
                    return Math.Sign(x.Number - y.Number);
                }
                else if (x.Type == MoonSharp.Interpreter.DataType.Boolean &&
                         y.Type == MoonSharp.Interpreter.DataType.Boolean)
                {
                    return x.Boolean != y.Boolean ? (x.Boolean ? 1 : -1) : 0;
                }

                return 0; // can't sort but don't error boi
            }
        }

        protected virtual Layer CreateNewLuaLayer(string layerPath, DynValue[] args) => new Layer(ScriptExecutionEnvironment, ResourceLocator, layerPath, args);

        [MoonSharpVisible(true)]
        private void ConnectAll(Table table)
        {
            foreach (var pair in table.Pairs)
            {
                GetEventFromKey(pair.Key.CastToString())?.Connect(pair.Value);
            }

            LuaBindableEvent? GetEventFromKey(string key) =>
                typeof(Layer).GetField("Lua" + key + "Event", BindingFlags.NonPublic | BindingFlags.Instance) is FieldInfo f ? (LuaBindableEvent)f.GetValue(this) : null;
        }

#region Internal Event Entry Points

        internal void InitializeInternal()
        {
            OnInitialize();
            lifetimeState = LifetimeState.Alive;
        }

        internal void DestroyInternal()
        {
            lifetimeState = LifetimeState.Destroyed;
            OnDestroy();
        }

        internal void SuspendInternal(Layer nextLayer)
        {
            if (IsSuspended) return;
            IsSuspended = true;

            OnSuspend(nextLayer);
        }

        internal void ResumeInternal(Layer previousLayer)
        {
            if (!IsSuspended) return;
            IsSuspended = false;

            OnResume(previousLayer);
        }

#endregion

        protected void CloseCurtain(float holdTime, Action? onClosed = null) => Client.CloseCurtain(holdTime, onClosed);
        protected void CloseCurtain(Action? onClosed = null) => Client.CloseCurtain(onClosed);
        protected void OpenCurtain() => Client.OpenCurtain();

        public void Push(Layer nextLayer) => Client.LayerStack.Push(this, nextLayer);
        public void Pop() => Client.LayerStack.Pop(this);

        public void SetInvalidForResume() => validForResume = false;

#region Overloadable Event Callbacks

        /// <summary>
        /// Called whenever the client window size is changed, even if this layer is suspended.
        /// If being suspended is important, check <see cref="IsSuspended"/>.
        /// </summary>
        [MoonSharpHidden]
        public virtual void ClientSizeChanged(int width, int height)
        {
            //m_script.Call(tblTheoriLayer["onClientSizeChanged"], width, height);
        }

        /// <summary>
        /// If a driving script was specified for this Layer, it is loaded and constructed.
        /// </summary>
        [MoonSharpHidden]
        public virtual bool AsyncLoad()
        {
            return LuaAsyncLoadEvent.Fire(this).Aggregate(true, (a, b) => a && b.CastToBool());
        }

        [MoonSharpHidden]
        public virtual bool AsyncFinalize()
        {
            return LuaAsyncFinalizeEvent.Fire(this).Aggregate(true, (a, b) => a && b.CastToBool());
        }

        [MoonSharpHidden]
        public virtual void OnInitialize()
        {
            //m_script.Call(tblTheoriLayer["init"]);
            LuaInitializeEvent.Fire(this);
        }

        [MoonSharpHidden]
        public virtual void OnDestroy()
        {
            //m_script.Call(tblTheoriLayer["destroy"]);
            //m_script.Dispose();
            LuaDestroyEvent.Fire(this);
        }

        /// <summary>
        /// Called when another layer which hides lower layers is placed anywhere above this layer.
        /// This will not be called if the layer is already in a suspended state.
        /// This will be called at the beginning of a frame, before inputs and updates, allowing the layer to
        ///  properly pause or destroy state after completing a frame.
        /// </summary>
        [MoonSharpHidden]
        public virtual void OnSuspend(Layer nextLayer)
        {
            //m_script.Call(tblTheoriLayer["suspended"]);
            LuaSuspendEvent.Fire(this);
        }

        /// <summary>
        /// Called when another layer which hides lower layers is removed from anywhere above this layer.
        /// This will not be invoked if this layer is already in an active state.
        /// </summary>
        [MoonSharpHidden]
        public virtual void OnResume(Layer previousLayer)
        {
            //m_script.Call(tblTheoriLayer["resumed"]);
            LuaResumeEvent.Fire(this);
        }

        /// <summary>
        /// Returns true to cancel the exit, false to continue.
        /// </summary>
        [MoonSharpHidden]
        public virtual bool OnExiting(Layer? source)
        {
            //return m_script.Call(tblTheoriLayer["onExiting"])?.CastToBool() ?? false;

            // TODO(local): figure out how to do the return here
            LuaExitingEvent.Fire(this);

            return false;
        }

        [MoonSharpHidden]
        public virtual void KeyPressed(KeyInfo info) => LuaKeyPressEvent.Fire(this, info.KeyCode);
        [MoonSharpHidden]
        public virtual void KeyReleased(KeyInfo info) => LuaKeyReleaseEvent.Fire(this, info.KeyCode);

        [MoonSharpHidden]
        public virtual void MouseButtonPressed(MouseButtonInfo info) => LuaMousePressEvent.Fire(this, info.Button);
        [MoonSharpHidden]
        public virtual void MouseButtonReleased(MouseButtonInfo info) => LuaMouseReleaseEvent.Fire(this, info.Button);
        [MoonSharpHidden]
        public virtual void MouseWheelScrolled(int dx, int dy) => LuaMouseScrollEvent.Fire(this, dx, dy);
        [MoonSharpHidden]
        public virtual void MouseMoved(int x, int y, int dx, int dy) => LuaMouseMoveEvent.Fire(this, x, y, dx, dy);

        [MoonSharpHidden]
        public virtual void GamepadConnected(Gamepad gamepad) => LuaGamepadConnectEvent.Fire(this, gamepad);
        [MoonSharpHidden]
        public virtual void GamepadDisconnected(Gamepad gamepad) => LuaGamepadDisconnectEvent.Fire(this, gamepad);
        [MoonSharpHidden]
        public virtual void GamepadButtonPressed(GamepadButtonInfo info) => LuaGamepadPressEvent.Fire(this, info.Button);
        [MoonSharpHidden]
        public virtual void GamepadButtonReleased(GamepadButtonInfo info) => LuaGamepadReleaseEvent.Fire(this, info.Button);
        [MoonSharpHidden]
        public virtual void GamepadAxisChanged(GamepadAxisInfo info) => LuaGamepadAxisChangeEvent.Fire(this, info.Axis, info.Value);
        [MoonSharpHidden]
        public virtual void GamepadBallChanged(GamepadBallInfo info) { }

        [MoonSharpHidden]
        public virtual void ControllerAdded(Controller controller) => LuaControllerAddEvent.Fire(this, controller);
        [MoonSharpHidden]
        public virtual void ControllerRemoved(Controller controller) => LuaControllerRemoveEvent.Fire(this, controller);
        [MoonSharpHidden]
        public virtual void ControllerButtonPressed(ControllerButtonInfo info) => LuaControllerPressEvent.Fire(this, info.Controller, info.Button);
        [MoonSharpHidden]
        public virtual void ControllerButtonReleased(ControllerButtonInfo info) => LuaControllerReleaseEvent.Fire(this, info.Controller, info.Button);
        [MoonSharpHidden]
        public virtual void ControllerAxisChanged(ControllerAxisInfo info) => LuaControllerAxisChangeEvent.Fire(this, info.Controller, info.Axis, info.Value);
        [MoonSharpHidden]
        public virtual void ControllerAxisTicked(ControllerAxisTickInfo info) => LuaControllerAxisTickEvent.Fire(this, info.Controller, info.Axis, info.Direction);

        [MoonSharpHidden]
        public virtual void Update(float delta, float total)
        {
            m_resources.Update();

            LuaUpdateEvent.Fire(this, delta, total);

            //m_script.Call(tblTheoriLayer["update"], delta, total);
            //m_script.CallIfExists("update", delta, total);
        }
        [MoonSharpHidden]
        public virtual void FixedUpdate(float delta, float total) { }
        [MoonSharpHidden]
        public virtual void LateUpdate() { }

        [MoonSharpHidden]
        public virtual void Render()
        {
            //m_spriteRenderer.BeginFrame();
            
            LuaRenderEvent.Fire(this);

#if false
            using var batch = m_renderer2D.Use();
            m_batch = batch;

            m_script.Call(tblTheoriLayer["render"]);
#endif

            //m_spriteRenderer.EndFrame();
        }
        [MoonSharpHidden]
        public virtual void LateRender() { }

#endregion
    }
}
