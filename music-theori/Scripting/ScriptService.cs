using System;
using System.Numerics;
using System.Reflection;

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

            RegisterType<Anchor>();

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
