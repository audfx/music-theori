using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using MoonSharp.Interpreter;
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
    public class Layer : IAsyncLoadable
    {
        private static readonly Dictionary<string, VectorFont> staticFonts = new Dictionary<string, VectorFont>();

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

        public bool IsSuspended { get; private set; } = false;

        internal bool validForResume = true;

        #endregion

        #region Platform

        private Client? m_client = null;
        public Client Client => m_client ?? throw new InvalidOperationException("Layer has not been initialized with a client yet.");

        public T ClientAs<T>() where T : Client => (T)Client;

        public ClientHost Host => Client.Host;

        public T HostAs<T>() where T : ClientHost => (T)Host;

        internal void SetClient(Client client)
        {
            if (m_client != null) throw new InvalidOperationException("Layer already has a client assigned to it.");
            m_client = client;
        }

        #endregion

        #region Layer Configuration

        public virtual int TargetFrameRate => 0;

        public virtual bool BlocksParentLayer => true;

        #endregion

        #region Resource Management

        public readonly ClientResourceLocator ResourceLocator;
        protected readonly ClientResourceManager m_resources;

        #endregion

        #region Scripting Interface

        private readonly BasicSpriteRenderer m_spriteRenderer;
        private readonly RenderBatch2D m_renderer2D;
        private RenderBatcher2D? m_batch = null;

        protected readonly ScriptProgram m_script;

        private string? m_drivingScriptFileName = null;
        private DynValue[]? m_drivingScriptArgs = null;

        public readonly Table tblTheori;
        public readonly Table tblTheoriAudio;
        public readonly Table tblTheoriCharts;
        public readonly Table tblTheoriConfig;
        public readonly Table tblTheoriGame;
        public readonly Table tblTheoriGraphics;
        public readonly Table tblTheoriInput;
        public readonly Table tblTheoriLayer;
        public readonly Table tblTheoriModes;
        public readonly Table tblTheoriInputKeyboard;
        public readonly Table tblTheoriInputMouse;
        public readonly Table tblTheoriInputGamepad;
        public readonly Table tblTheoriInputController;

        public readonly ScriptEvent evtKeyPressed, evtRawKeyPressed;
        public readonly ScriptEvent evtKeyReleased, evtRawKeyReleased;

        public readonly ScriptEvent evtMousePressed, evtRawMousePressed;
        public readonly ScriptEvent evtMouseReleased, evtRawMouseReleased;
        public readonly ScriptEvent evtMouseMoved, evtRawMouseMoved;
        public readonly ScriptEvent evtMouseScrolled, evtRawMouseScrolled;

        public readonly ScriptEvent evtGamepadConnected;
        public readonly ScriptEvent evtGamepadDisconnected;
        public readonly ScriptEvent evtGamepadPressed, evtRawGamepadPressed;
        public readonly ScriptEvent evtGamepadReleased, evtRawGamepadReleased;
        public readonly ScriptEvent evtGamepadAxisChanged, evtRawGamepadAxisChanged;

        public readonly ScriptEvent evtControllerAdded;
        public readonly ScriptEvent evtControllerRemoved;
        public readonly ScriptEvent evtControllerPressed, evtRawControllerPressed;
        public readonly ScriptEvent evtControllerReleased, evtRawControllerReleased;
        public readonly ScriptEvent evtControllerAxisChanged, evtRawControllerAxisChanged;
        public readonly ScriptEvent evtControllerAxisTicked, evtRawControllerAxisTicked;

        #endregion

        protected ClientResourceManager StaticResources => theori.Host.StaticResources;

        public Layer(ClientResourceLocator? resourceLocator = null, string? layerPathLua = null, params DynValue[] args)
        {
            ResourceLocator = resourceLocator ?? ClientResourceLocator.Default;
            m_spriteRenderer = new BasicSpriteRenderer(ResourceLocator);

            m_resources = new ClientResourceManager(ResourceLocator);
            m_script = new ScriptProgram(ResourceLocator);

            m_renderer2D = new RenderBatch2D(m_resources);

            m_drivingScriptFileName = layerPathLua;
            m_drivingScriptArgs = args;

            m_script["KeyCode"] = typeof(KeyCode);
            m_script["MouseButton"] = typeof(MouseButton);
            m_script["ControllerAxisStyle"] = typeof(ControllerAxisStyle);

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

            tblTheoriInputKeyboard["isDown"] = (Func<KeyCode, bool>)(key => UserInputService.IsKeyDown(key));
            tblTheoriInputKeyboard["pressed"] = evtKeyPressed = m_script.NewEvent();
            tblTheoriInputKeyboard["released"] = evtKeyReleased = m_script.NewEvent();
            tblTheoriInputKeyboard["pressedRaw"] = evtRawKeyPressed = m_script.NewEvent();
            tblTheoriInputKeyboard["releasedRaw"] = evtRawKeyReleased = m_script.NewEvent();

            tblTheoriInputMouse["pressed"] = evtMousePressed = m_script.NewEvent();
            tblTheoriInputMouse["released"] = evtMouseReleased = m_script.NewEvent();
            tblTheoriInputMouse["moved"] = evtMouseMoved = m_script.NewEvent();
            tblTheoriInputMouse["scrolled"] = evtMouseScrolled = m_script.NewEvent();
            tblTheoriInputMouse["pressedRaw"] = evtRawMousePressed = m_script.NewEvent();
            tblTheoriInputMouse["releasedRaw"] = evtRawMouseReleased = m_script.NewEvent();
            tblTheoriInputMouse["movedRaw"] = evtRawMouseMoved = m_script.NewEvent();
            tblTheoriInputMouse["scrolledRaw"] = evtRawMouseScrolled = m_script.NewEvent();
            tblTheoriInputMouse["getMousePosition"] = (Func<DynValue>)(() => NewTuple(NewNumber(UserInputService.MouseX), NewNumber(UserInputService.MouseY)));

            tblTheoriInputGamepad["connected"] = evtGamepadConnected = m_script.NewEvent();
            tblTheoriInputGamepad["disconnected"] = evtGamepadDisconnected = m_script.NewEvent();
            tblTheoriInputGamepad["pressed"] = evtGamepadPressed = m_script.NewEvent();
            tblTheoriInputGamepad["released"] = evtGamepadReleased = m_script.NewEvent();
            tblTheoriInputGamepad["axisChanged"] = evtGamepadAxisChanged = m_script.NewEvent();
            tblTheoriInputGamepad["pressedRaw"] = evtRawGamepadPressed = m_script.NewEvent();
            tblTheoriInputGamepad["releasedRaw"] = evtRawGamepadReleased = m_script.NewEvent();
            tblTheoriInputGamepad["axisChangedRaw"] = evtRawGamepadAxisChanged = m_script.NewEvent();

            tblTheoriInputController["added"] = evtControllerAdded = m_script.NewEvent();
            tblTheoriInputController["removed"] = evtControllerRemoved = m_script.NewEvent();
            tblTheoriInputController["pressed"] = evtControllerPressed = m_script.NewEvent();
            tblTheoriInputController["released"] = evtControllerReleased = m_script.NewEvent();
            tblTheoriInputController["axisChanged"] = evtControllerAxisChanged = m_script.NewEvent();
            tblTheoriInputController["axisTicked"] = evtControllerAxisTicked = m_script.NewEvent();

            tblTheori["doStaticLoadsAsync"] = (Func<bool>)(() => StaticResources.LoadAll());
            tblTheori["finalizeStaticLoads"] = (Func<bool>)(() => StaticResources.FinalizeLoad());

            tblTheoriAudio["queueStaticAudioLoad"] = (Func<string, AudioHandle>)(audioName => StaticResources.QueueAudioLoad($"audio/{ audioName }"));
            tblTheoriAudio["getStaticAudio"] = (Func<string, AudioHandle>)(audioName => StaticResources.GetAudio($"audio/{ audioName }"));
            tblTheoriAudio["queueAudioLoad"] = (Func<string, AudioHandle>)(audioName => m_resources.QueueAudioLoad($"audio/{ audioName }"));
            tblTheoriAudio["getAudio"] = (Func<string, AudioHandle>)(audioName => m_resources.GetAudio($"audio/{ audioName }"));
            tblTheoriAudio["createFakeAudio"] = (Func<int, int, AudioHandle>)((sampleRate, channels) => new FakeAudioSource(sampleRate, channels));
            
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
            tblTheoriConfig["save"] = (Action)(() => UserConfigManager.SaveToFile());

            tblTheoriGame["exit"] = (Action)(() => Host.Exit());

            tblTheoriGraphics["queueStaticTextureLoad"] = (Func<string, Texture>)(textureName => StaticResources.QueueTextureLoad($"textures/{ textureName }"));
            tblTheoriGraphics["getStaticTexture"] = (Func<string, Texture>)(textureName => StaticResources.GetTexture($"textures/{ textureName }"));
            tblTheoriGraphics["queueTextureLoad"] = (Func<string, Texture>)(textureName => m_resources.QueueTextureLoad($"textures/{ textureName }"));
            tblTheoriGraphics["getTexture"] = (Func<string, Texture>)(textureName => m_resources.GetTexture($"textures/{ textureName }"));
            tblTheoriGraphics["createStaticFont"] = (Func<string, VectorFont?>)(fontName => ResourceLocator.OpenFileStreamWithExtension($"fonts/{ fontName }", new[] { ".ttf", ".otf" }, out string _) is Stream fs ? staticFonts[fontName] = new VectorFont(fs) : null);
            tblTheoriGraphics["createFont"] = (Func<string, VectorFont?>)(fontName => ResourceLocator.OpenFileStreamWithExtension($"fonts/{ fontName }", new[] { ".ttf", ".otf" }, out string _) is Stream fs ? new VectorFont(fs) : null);
            tblTheoriGraphics["getStaticFont"] = (Func<string, VectorFont?>)(fontName => staticFonts.TryGetValue(fontName, out var font) ? font : null);
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
            tblTheoriGraphics["measureString"] = (Func<string, DynValue>)(text => { var bounds = m_batch?.MeasureString(text)!.Value; return NewTuple(NewNumber(bounds.X), NewNumber(bounds.Y)); });
            tblTheoriGraphics["fillPathAt"] = (Action<Path2DCommands, float, float, float, float>)((path, x, y, sx, sy) => m_batch?.FillPathAt(path, x, y, sx, sy));
            tblTheoriGraphics["saveScissor"] = (Action)(() => m_batch?.SaveScissor());
            tblTheoriGraphics["restoreScissor"] = (Action)(() => m_batch?.RestoreScissor());
            tblTheoriGraphics["resetScissor"] = (Action)(() => m_batch?.ResetScissor());
            tblTheoriGraphics["scissor"] = (Action<float, float, float, float>)((x, y, w, h) => m_batch?.Scissor(x, y, w, h));

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
            tblTheoriInput["beginTextInput"] = (Action)(() => UserInputService.BeginTextEditing());
            tblTheoriInput["endTextInput"] = (Action)(() => UserInputService.EndTextEditing());
            tblTheoriInput["isInputModeEnabled"] = (Func<string, bool>)(modeName => UserInputService.InputModes.HasFlag(GetModeFromString(modeName)));

            tblTheoriInput["getControllerFiles"] = (Func<string[]>)(() => Directory.GetFiles("controller", "*.json"));
            tblTheoriInput["loadController"] = (Func<string, Controller?>)(fileName => Controller.TryCreateFromFile(Path.Combine("controllers", fileName)));
            tblTheoriInput["saveController"] = (Action<Controller>)(con => con.SaveToFile());

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

        protected virtual Layer CreateNewLuaLayer(string layerPath, DynValue[] args) => new Layer(ResourceLocator, layerPath, args);

#region Internal Event Entry Points

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

#endregion

#region Script API

        protected Table Script_AddToTheoriNamespace(params string[] subNames)
        {
            if (subNames.Length == 0)
                throw new InvalidOperationException("Sub-namespaces expected, none given.");

            Table result = tblTheori;
            foreach (string subName in subNames)
                result = (Table)(result[subName] = m_script.NewTable());

            return result;
        }

#endregion

        protected void CloseCurtain(float holdTime, Action? onClosed = null) => Client.CloseCurtain(holdTime, onClosed);
        protected void CloseCurtain(Action? onClosed = null) => Client.CloseCurtain(onClosed);
        protected void OpenCurtain() => Client.OpenCurtain();

        protected void Push(Layer nextLayer) => Client.LayerStack.Push(this, nextLayer);
        protected void Pop() => Client.LayerStack.Pop(this);

        protected void SetInvalidForResume() => validForResume = false;

#region Overloadable Event Callbacks

        /// <summary>
        /// Called whenever the client window size is changed, even if this layer is suspended.
        /// If being suspended is important, check <see cref="IsSuspended"/>.
        /// </summary>
        public virtual void ClientSizeChanged(int width, int height)
        {
            m_script.Call(tblTheoriLayer["onClientSizeChanged"], width, height);
        }

        /// <summary>
        /// If a driving script was specified for this Layer, it is loaded and constructed.
        /// </summary>
        public virtual bool AsyncLoad()
        {
            if (m_drivingScriptFileName is string scriptFile)
            {
                m_script.LoadScriptResourceFile(scriptFile);
                m_script.Call(tblTheoriLayer["construct"], m_drivingScriptArgs!);

                var result = m_script.Call(tblTheoriLayer["doAsyncLoad"]);

                if (result == null) return true; // guard against function missing
                if (!result.CastToBool()) return false;

                if (!m_resources.LoadAll()) return false;
            }

            return true;
        }

        public virtual bool AsyncFinalize()
        {
            if (m_drivingScriptFileName is string _)
            {
                var result = m_script.Call(tblTheoriLayer["doAsyncFinalize"]);

                if (result == null) return true; // guard against function missing
                if (!result.CastToBool()) return false;

                if (!m_resources.FinalizeLoad()) return false;
            }

            return true;
        }

        public virtual void Initialize()
        {
            m_script.Call(tblTheoriLayer["init"]);
        }

        public virtual void Destroy()
        {
            m_script.Call(tblTheoriLayer["destroy"]);
            m_script.Dispose();
        }

        /// <summary>
        /// Called when another layer which hides lower layers is placed anywhere above this layer.
        /// This will not be called if the layer is already in a suspended state.
        /// This will be called at the beginning of a frame, before inputs and updates, allowing the layer to
        ///  properly pause or destroy state after completing a frame.
        /// </summary>
        public virtual void Suspended(Layer nextLayer)
        {
            m_script.Call(tblTheoriLayer["suspended"]);
        }

        /// <summary>
        /// Called when another layer which hides lower layers is removed from anywhere above this layer.
        /// This will not be invoked if this layer is already in an active state.
        /// </summary>
        public virtual void Resumed(Layer previousLayer)
        {
            m_script.Call(tblTheoriLayer["resumed"]);
        }

        /// <summary>
        /// Returns true to cancel the exit, false to continue.
        /// </summary>
        public virtual bool OnExiting(Layer? source) => m_script.Call(tblTheoriLayer["onExiting"])?.CastToBool() ?? false;

        public virtual void KeyPressed(KeyInfo info) => evtKeyPressed.Fire(info.KeyCode);
        public virtual void KeyReleased(KeyInfo info) => evtKeyReleased.Fire(info.KeyCode);
        public virtual void RawKeyPressed(KeyInfo info) => evtRawKeyPressed.Fire(info.KeyCode);
        public virtual void RawKeyReleased(KeyInfo info) => evtRawKeyReleased.Fire(info.KeyCode);

        public virtual void MouseButtonPressed(MouseButtonInfo info) => evtMousePressed.Fire(info.Button, UserInputService.MouseX, UserInputService.MouseY);
        public virtual void MouseButtonReleased(MouseButtonInfo info) => evtMouseReleased.Fire(info.Button, UserInputService.MouseX, UserInputService.MouseY);
        public virtual void MouseWheelScrolled(int dx, int dy) => evtMouseScrolled.Fire(dx, dy);
        public virtual void MouseMoved(int x, int y, int dx, int dy) => evtMouseMoved.Fire(x, y, dx, dy);
        public virtual void RawMouseButtonPressed(MouseButtonInfo info) => evtRawMousePressed.Fire(info.Button, UserInputService.MouseX, UserInputService.MouseY);
        public virtual void RawMouseButtonReleased(MouseButtonInfo info) => evtRawMouseReleased.Fire(info.Button, UserInputService.MouseX, UserInputService.MouseY);
        public virtual void RawMouseWheelScrolled(int dx, int dy) => evtRawMouseScrolled.Fire(dx, dy);
        public virtual void RawMouseMoved(int x, int y, int dx, int dy) => evtRawMouseMoved.Fire(x, y, dx, dy);

        public virtual void GamepadConnected(Gamepad gamepad) => evtGamepadConnected.Fire(gamepad);
        public virtual void GamepadDisconnected(Gamepad gamepad) => evtGamepadDisconnected.Fire(gamepad);
        public virtual void GamepadButtonPressed(GamepadButtonInfo info) => evtGamepadPressed.Fire(info.Gamepad, info.Button);
        public virtual void GamepadButtonReleased(GamepadButtonInfo info) => evtGamepadReleased.Fire(info.Gamepad, info.Button);
        public virtual void GamepadAxisChanged(GamepadAxisInfo info) => evtGamepadAxisChanged.Fire(info.Gamepad, info.Axis, info.Value);
        public virtual void GamepadBallChanged(GamepadBallInfo info) => evtGamepadAxisChanged.Fire(info.Gamepad, info.Ball, info.XRelative, info.YRelative);
        public virtual void RawGamepadButtonPressed(GamepadButtonInfo info) => evtRawGamepadPressed.Fire(info.Gamepad, info.Button);
        public virtual void RawGamepadButtonReleased(GamepadButtonInfo info) => evtRawGamepadReleased.Fire(info.Gamepad, info.Button);
        public virtual void RawGamepadAxisChanged(GamepadAxisInfo info) => evtRawGamepadAxisChanged.Fire(info.Gamepad, info.Axis, info.Value);
        public virtual void RawGamepadBallChanged(GamepadBallInfo info) => evtRawGamepadAxisChanged.Fire(info.Gamepad, info.Ball, info.XRelative, info.YRelative);

        public virtual void ControllerAdded(Controller controller) => evtControllerAdded.Fire(controller);
        public virtual void ControllerRemoved(Controller controller) => evtControllerRemoved.Fire(controller);
        public virtual void ControllerButtonPressed(ControllerButtonInfo info) => evtControllerPressed.Fire(info.Controller, info.Button.ToObject());
        public virtual void ControllerButtonReleased(ControllerButtonInfo info) => evtControllerReleased.Fire(info.Controller, info.Button.ToObject());
        public virtual void ControllerAxisChanged(ControllerAxisInfo info) => evtControllerAxisChanged.Fire(info.Controller, info.Axis.ToObject(), info.Value, info.Delta);
        public virtual void ControllerAxisTicked(ControllerAxisTickInfo info) => evtControllerAxisTicked.Fire(info.Controller, info.Axis.ToObject(), info.Direction);

        public virtual void Update(float delta, float total)
        {
            m_resources.Update();
            m_script.Call(tblTheoriLayer["update"], delta, total);
            //m_script.CallIfExists("update", delta, total);
        }
        public virtual void FixedUpdate(float delta, float total) { }
        public virtual void LateUpdate() { }

        public virtual void Render()
        {
            //m_spriteRenderer.BeginFrame();

            using var batch = m_renderer2D.Use();
            m_batch = batch;

            m_script.Call(tblTheoriLayer["render"]);

            //m_spriteRenderer.EndFrame();
        }
        public virtual void LateRender() { }

#endregion
    }
}
