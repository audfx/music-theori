using System.Collections.Generic;

using theori.Charting;
using theori.Database;
using theori.Resources;

using MoonSharp;
using MoonSharp.Interpreter;

using static MoonSharp.Interpreter.DynValue;
using System.Linq;
using System;
using theori.Graphics.OpenGL;
using System.IO;
using theori.Configuration;
using System.Diagnostics;
using System.Text;
using theori.Audio;

namespace theori.Scripting
{
    public sealed class ScriptChartDatabaseService : BaseScriptInstance
    {
        public static readonly ScriptChartDatabaseService Instance = new ScriptChartDatabaseService();

        private ScriptChartDatabaseService()
        {
        }

        public IEnumerable<ChartSetInfo> GetChartSets() => ChartDatabaseService.ChartSets;
        public IEnumerable<ChartInfo> GetCharts() => ChartDatabaseService.Charts;
    }

    public abstract class LuaObjectHandle<T> : BaseScriptInstance
    {
        public static implicit operator T(LuaObjectHandle<T> handle) => handle.Object;

        protected readonly T Object;

        protected LuaObjectHandle(T obj)
        {
            Object = obj;
        }
    }

    public class AudioHandle : LuaObjectHandle<AudioTrack>
    {
        public static implicit operator AudioHandle(AudioTrack audio) => new AudioHandle(audio);

        public AudioHandle(AudioTrack audio)
            : base(audio)
        {
            audio.Channel = Mixer.MasterChannel;
            audio.RemoveFromChannelOnFinish = false;
        }

        public double Volume
        {
            get => Object.Volume;
            set => Object.Volume = (float)value;
        }

        public double Position
        {
            get => Object.Position.Seconds;
            set => Object.Position = value;
        }

        public bool IsPlaying => Object.PlaybackState == PlaybackState.Playing;

        public void SetLoopAreaSamples(long start, long end) => Object.SetLoopArea(start, end);
        public void SetLoopArea(double startTime, double endTime) => Object.SetLoopArea(startTime, endTime);

        public void RemoveLoopArea() => Object.RemoveLoopArea();

        public void Play() => Object.Play();
        public void Stop() => Object.Stop();

        public void PlayFromStart() => Object.Replay();
    }

    public sealed class ChartSetInfoHandle : LuaObjectHandle<ChartSetInfo>
    {
        //public static implicit operator NscChartSetInfoHandle(ChartSetInfo audio) => new NscChartSetInfoHandle(audio);

        internal readonly ClientResourceManager Resources;
        internal readonly ScriptProgram Script;
        internal readonly ChartDatabaseWorker Worker;

        private List<ChartInfoHandle>? m_charts;

        public ChartSetInfoHandle(ClientResourceManager resources, ScriptProgram script, ChartDatabaseWorker worker, ChartSetInfo info)
            : base(info)
        {
            Resources = resources;
            Script = script;
            Worker = worker;
        }

        /// <summary>
        /// The database primary key.
        /// </summary>
        public long ID => Object.ID;

        public long LastWriteTime => Object.LastWriteTime;

        public long? OnlineID => Object.OnlineID;

        /// <summary>
        /// Parent path relative to the selected chart storage directory.
        /// </summary>
        public string FilePath => Object.FilePath;

        /// <summary>
        /// The name of the chart set file.
        /// </summary>
        public string FileName => Object.FileName;

        public List<ChartInfoHandle> Charts => m_charts ?? (m_charts = Object.Charts
            .OrderBy(info => info.DifficultyIndex ?? 0)
            .ThenBy(info => info.DifficultyLevel)
            .ThenBy(info => info.DifficultyName)
            .ThenBy(info => info.SongTitle)
            .Select(info => new ChartInfoHandle(this, info)).ToList());
    }

    public sealed class ChartSetInfoSubsection : BaseScriptInstance
    {
        public ChartSetInfoHandle Set { get; }

        private readonly Dictionary<int, List<ChartInfoHandle>> m_charts = new Dictionary<int, List<ChartInfoHandle>>();

        public List<ChartInfoHandle> this[int difficultyIndex]
        {
            get
            {
                if (!m_charts.TryGetValue(difficultyIndex, out var diffs))
                    m_charts[difficultyIndex] = diffs = new List<ChartInfoHandle>();
                return diffs;
            }
        }

        public ChartSetInfoSubsection(ChartSetInfoHandle chartSet, IEnumerable<ChartInfoHandle> relevantCharts)
        {
            Set = chartSet;
            foreach (var chart in relevantCharts)
                this[chart.DifficultyIndex].Add(chart);
        }

        public ChartSetInfoSubsection(ChartSetInfoHandle chartSet, params ChartInfoHandle[] relevantCharts)
        {
            Set = chartSet;
            foreach (var chart in relevantCharts)
                this[chart.DifficultyIndex].Add(chart);
        }

        [MoonSharpHidden]
        internal void Concat(ChartSetInfoSubsection other)
        {
            foreach (var (key, value) in other.m_charts)
            {
                foreach (var chart in value)
                    this[key].Add(chart);
            }
        }
    }

    public sealed class ChartInfoHandle : LuaObjectHandle<ChartInfo>
    {
        internal ClientResourceManager Resources => Set.Resources;
        internal ScriptProgram Script => Set.Script;
        internal ChartDatabaseWorker Worker => Set.Worker;

        private Texture? m_jacketTexture;

        public ChartInfoHandle(ChartSetInfoHandle setInfo, ChartInfo info)
            : base(info)
        {
            Set = setInfo;
        }

        /// <summary>
        /// The database primary key.
        /// </summary>
        public long ID => Object.ID;

        public long LastWriteTime => Object.LastWriteTime;

        public long SetID => Object.SetID;
        public ChartSetInfoHandle Set { get; private set; }

        /// <summary>
        /// The name of the chart file inside of the Set directory.
        /// </summary>
        public string FileName => Object.FileName;

        public string SongTitle => Object.SongTitle;
        public string SongArtist => Object.SongArtist;
        public string SongFileName => Object.SongFileName;
        public int SongVolume => Object.SongVolume;

        public double ChartOffset => Object.ChartOffset.Seconds;

        public string Charter => Object.Charter;

        public string JacketFileName => Object.JacketFileName;
        public string JacketArtist => Object.JacketArtist;

        public string BackgroundFileName => Object.BackgroundFileName;
        public string BackgroundArtist => Object.BackgroundArtist;

        public double DifficultyLevel => Object.DifficultyLevel;
        public int DifficultyIndex => 1 + (Object.DifficultyIndex ?? 0);

        public string DifficultyName => Object.DifficultyName;
        public string DifficultyNameShort => Object.DifficultyNameShort;

        public DynValue DifficultyColor => Object.DifficultyColor is { } c ?
            NewTuple(NewNumber(255 * c.X), NewNumber(255 * c.Y), NewNumber(255 * c.Z)) :
            NewTuple(NewNumber(255), NewNumber(255), NewNumber(255));

        public double ChartDuration => Object.ChartDuration.Seconds;

        public string Tags => Object.Tags;

        public bool HasJacketTexture => GetJacketTexture() != Texture.Empty;

        public Texture GetJacketTexture()
        {
            if (m_jacketTexture is { } result) return result;

            m_jacketTexture = Texture.Empty;
            try
            {
                string chartsDir = ChartDatabaseService.ChartsDirectory;
                string texturePath = Path.Combine(chartsDir, Object.Set.FilePath, Object.JacketFileName);

                Texture? actualTexture = null;
                actualTexture = Resources.LoadTexture(File.OpenRead(texturePath), Path.GetExtension(Object.JacketFileName), () =>
                {
                    m_jacketTexture = actualTexture!;
                    Resources.Manage(actualTexture!);
                });
            }
            catch (Exception) { }

            return m_jacketTexture;
        }

        public DynValue GetConfig()
        {
            string configString = ChartDatabaseService.GetLocalConfigForChart(Object);

            string[] entries = configString.Split(';');
            var config = Script.NewTable();

            foreach (string entry in entries)
            {
                static DynValue Parse(string v)
                {
                    if (v.StartsWith('"'))
                    {
                        Debug.Assert(v.EndsWith('"'));
                        return NewString(v[1..^1]);
                    }
                    else switch (v)
                    {
                        case "true": return True;
                        case "false": return False;
                        case "nil": return Nil;

                        default:
                        {
                            if (double.TryParse(v, out double vNumber))
                                return NewNumber(vNumber);
                            else goto case "nil";
                        }
                    }
                }

                if (entry.TrySplit('=', out string key, out string value))
                    config[key] = Parse(value);
                else config.Append(Parse(value));
            }

            return NewTable(config);
        }

        public void SetConfig(DynValue configValue)
        {
            if (configValue.Type != MoonSharp.Interpreter.DataType.Table)
                return;

            var config = configValue.Table;

            var configString = new StringBuilder();
            for (int i = 0; i < config.Length; i++)
            {
                if (configString.Length > 0)
                    configString.Append(';');
                configString.Append(config.Get(i));
            }
            foreach (var pair in config.Pairs)
            {
                if (configString.Length > 0)
                    configString.Append(';');

                switch (pair.Value.Type)
                {
                    case MoonSharp.Interpreter.DataType.Tuple:
                    case MoonSharp.Interpreter.DataType.ClrFunction:
                    case MoonSharp.Interpreter.DataType.Function:
                    case MoonSharp.Interpreter.DataType.Table:
                    case MoonSharp.Interpreter.DataType.TailCallRequest:
                    case MoonSharp.Interpreter.DataType.Thread:
                    case MoonSharp.Interpreter.DataType.UserData:
                    case MoonSharp.Interpreter.DataType.Void:
                    case MoonSharp.Interpreter.DataType.YieldRequest:
                        continue;
                }

                // accepts nil, number, string, bool
                string valueString = pair.Value.Type switch
                {
                    MoonSharp.Interpreter.DataType.String => $"\"{ pair.Value.String }\"",
                    MoonSharp.Interpreter.DataType.Boolean => pair.Value.Boolean ? "true" : "false",
                    MoonSharp.Interpreter.DataType.Nil => "nil",
                    MoonSharp.Interpreter.DataType.Number => pair.Value.Number.ToString(),
                    _ => throw new NotImplementedException(pair.Value.Type.ToString()),
                };

                configString.Append($"{ pair.Key.ToPrintString() }={ valueString }");
            }

            ChartDatabaseService.SaveLocalConfigForChart(Object, configString.ToString());
        }
    }
}
