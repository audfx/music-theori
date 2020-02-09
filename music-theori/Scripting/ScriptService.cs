using System;
using System.Numerics;
using System.Reflection;

using theori.Audio;
using theori.Charting;
using theori.Graphics;
using theori.Graphics.OpenGL;
using theori.IO;
using theori.Resources;
using theori.Scoring;

using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace theori.Scripting
{
    public static class ScriptService
    {
        internal static void RegisterTheoriClrTypes()
        {
            using var _ = Profiler.Scope("ScriptService::RegisterTheoriClrTypes");

            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<tick_t>((s, v) => DynValue.NewNumber((double)v));
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<time_t>((s, v) => DynValue.NewNumber((double)v));
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<HybridLabel>((s, v) => v.LabelKind == HybridLabel.Kind.Number ? DynValue.NewNumber((int)v) : DynValue.NewString((string)v));

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.Number, typeof(tick_t), v => (tick_t)v.Number);
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.Number, typeof(time_t), v => (time_t)v.Number);
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.Number, typeof(HybridLabel), v => (HybridLabel)v.Number);
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.String, typeof(HybridLabel), v => (HybridLabel)v.String);
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.UserData, typeof(Chart), v => v.UserData.Object is ChartHandle handle ? handle.Chart : null);
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.UserData, typeof(AudioTrack), v => v.UserData.Object is AudioHandle handle ? handle.Object : null);

            RegisterType<Anchor>();
            RegisterType<LinearDirection>();
            RegisterType<AngularDirection>();
            RegisterType<ScoreRank>();

            RegisterType<Vector2>();
            RegisterType<Vector3>();
            RegisterType<Vector4>();

            RegisterType<Font>();
            RegisterType<VectorFont>();
            RegisterType<Texture>();
            RegisterType<TextRasterizer>();
            RegisterType<Path2DCommands>();

            RegisterType<KeyCode>();
            RegisterType<MouseButton>();
            RegisterType<Axis>();
            RegisterType<Gamepad>();
            RegisterType<Controller>();

            RegisterType<BasicSpriteRenderer>();
            RegisterType<ClientResourceManager>();

            RegisterType<ScriptEvent>();
            RegisterType<ScriptEvent.Connection>();

            RegisterType<Chart.ChartLane>();
            RegisterType<Chart.ControlPointList>();
            RegisterType<Entity>();
            RegisterType<ControlPoint>();

            RegisterType<ChartHandle>();
            RegisterType<AudioHandle>();
            RegisterType<ChartSetInfoHandle>();
            RegisterType<ChartInfoHandle>();
        }

        public static IUserDataDescriptor RegisterType<T>() => UserData.RegisterType<T>();
        public static void RegisterType(Type type) => UserData.RegisterType(type);

        public static void RegisterAssembly(Assembly? assembly = null, bool includeExtensionTypes = false) =>
            UserData.RegisterAssembly(assembly, includeExtensionTypes);
    }
}
