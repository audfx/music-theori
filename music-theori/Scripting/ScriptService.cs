using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;

using theori.Charting;
using theori.Graphics;
using theori.Graphics.OpenGL;
using theori.IO;
using theori.Resources;
using theori.Scoring;

using MoonSharp.Interpreter;

namespace theori.Scripting
{
    public static class ScriptService
    {
        internal static void RegisterTheoriClrTypes()
        {
            using var _ = Profiler.Scope("ScriptService::RegisterTheoriClrTypes");

            //RegisterType<tick_t>();
            //RegisterType<time_t>();
            //RegisterType<HybridLabel>();

            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<tick_t>((s, v) => DynValue.NewNumber((double)v));
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<time_t>((s, v) => DynValue.NewNumber((double)v));
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<HybridLabel>((s, v) => v.LabelKind == HybridLabel.Kind.Number ? DynValue.NewNumber((int)v) : DynValue.NewString((string)v));
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Chart>((s, v) => UserData.Create(new ChartHandle(v)));

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.Number, typeof(tick_t), v => (tick_t)v.Number);
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.Number, typeof(time_t), v => (time_t)v.Number);
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.Number, typeof(HybridLabel), v => (HybridLabel)v.Number);
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.String, typeof(HybridLabel), v => (HybridLabel)v.String);
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(MoonSharp.Interpreter.DataType.UserData, typeof(Chart), v => v.UserData.Object is ChartHandle handle ? handle.Chart : null);

            RegisterType<Anchor>();
            RegisterType<LinearDirection>();
            RegisterType<AngularDirection>();
            RegisterType<ScoreRank>();

            RegisterType<Vector2>();
            RegisterType<Vector3>();
            RegisterType<Vector4>();

            RegisterType<Font>();
            RegisterType<Texture>();
            RegisterType<TextRasterizer>();

            RegisterType<KeyCode>();
            RegisterType<MouseButton>();
            RegisterType<Axis>();
            RegisterType<Gamepad>();
            RegisterType<Controller>();

            RegisterType<BasicSpriteRenderer>();
            RegisterType<ClientResourceManager>();

            RegisterType<ScriptWindowInterface>();

            RegisterType<ScriptEvent>();
            RegisterType<ScriptEvent.Connection>();

            RegisterType<BaseScriptInstance>();
            RegisterType<ScriptDataModel>();
            RegisterType<ScriptResources>();

            RegisterType<Chart.ChartLane>();
            RegisterType<Chart.ControlPointList>();

            RegisterType<ChartHandle>();
            RegisterType<Entity>();
            RegisterType<ControlPoint>();

            RegisterType<AudioHandle>();
            RegisterType<ChartSetInfoHandle>();
            RegisterType<ChartSetInfoSubsection>();
            RegisterType<ChartInfoHandle>();
        }

        public static void RegisterType<T>() => UserData.RegisterType<T>();
        public static void RegisterType(Type type) => UserData.RegisterType(type);

        public static void RegisterAssembly(Assembly? assembly = null, bool includeExtensionTypes = false) =>
            UserData.RegisterAssembly(assembly, includeExtensionTypes);
    }
}
