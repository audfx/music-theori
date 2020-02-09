using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;

using theori.Graphics;
using theori.Resources;

using MoonSharp.Interpreter;
using theori.Scoring;

namespace theori.Scripting
{
    public class ScriptProgram : Disposable
    {
        private static readonly ClientResourceLocator scriptLocator;

        static ScriptProgram()
        {
            scriptLocator = ClientResourceLocator.Default;
            scriptLocator.AddManifestResourceLoader(ManifestResourceLoader.GetResourceLoader(typeof(ScriptProgram).Assembly, "theori.Resources"));
        }

        internal readonly Script Script;

        private readonly Dictionary<Type, DynValue> m_luaConverters = new Dictionary<Type, DynValue>();

        public readonly ClientResourceLocator ResourceLocator;
        public readonly ClientResourceManager Resources;

        private BasicSpriteRenderer? m_renderer;

        public object? this[string globalKey]
        {
            get => Script.Globals[globalKey];
            set
            {
                if (value != null && m_luaConverters.TryGetValue(value.GetType(), out var converter))
                    Script.Globals[globalKey] = Script.Call(converter, value);
                else Script.Globals[globalKey] = value;
            }
        }

        public ScriptProgram(ClientResourceLocator? resourceLocator = null)
        {
            ResourceLocator = resourceLocator ?? ClientResourceLocator.Default;
            Resources = new ClientResourceManager(ResourceLocator);

            Script = new Script(CoreModules.Basic
                                 | CoreModules.String
                                 | CoreModules.Bit32
                                 | CoreModules.Coroutine
                                 | CoreModules.Debug
                                 | CoreModules.ErrorHandling
                                 | CoreModules.GlobalConsts
                                 | CoreModules.Json
                                 | CoreModules.Math
                                 | CoreModules.Metatables
                                 | CoreModules.Table
                                 | CoreModules.IO
                                 | CoreModules.TableIterators);

            //InitBuiltInLibrary();
        }

        protected override void DisposeManaged()
        {
            Resources.Dispose();
        }

        #region Script API

        /// <summary>`include`</summary>
        public DynValue LoadScriptResourceFile(string fileName)
        {
            fileName = $"scripts/{ fileName }.lua";
            return LoadFile(ResourceLocator.OpenFileStream(fileName), fileName);
        }

        #endregion

        public void InitBuiltInLibrary()
        {
            //this["theori"] = ScriptDataModel.Instance;

            this["include"] = (Func<string, DynValue>)LoadScriptResourceFile;

            Script.Globals.Get("math").Table["sign"] = (Func<double, int>)Math.Sign;
            Script.Globals.Get("math").Table["clamp"] = (Func<double, double, double, double>)MathL.Clamp;

            Script.Globals.Get("table").Table["shallowCopy"] = (Func<DynValue, DynValue>)(table =>
            {
                var result = NewTable();
                foreach (var pair in table.Table.Pairs)
                    result[pair.Key] = pair.Value;
                return DynValue.NewTable(result);
            });

            // TODO(local): remove global resource manager!!!
            //Script.Globals["res"] = Resources;

            this["Anchor"] = typeof(Anchor);
            this["ScoreRank"] = typeof(ScoreRank);

            static Vector2 NewVec2(float x, float y) => new Vector2(x, y);
            this["vec2"] = (Func<float, float, Vector2>)NewVec2;

            static Vector3 NewVec3(float x, float y, float z) => new Vector3(x, y, z);
            this["vec3"] = (Func<float, float, float, Vector3>)NewVec3;

            static Vector4 NewVec4(float x, float y, float z, float w) => new Vector4(x, y, z, w);
            this["vec4"] = (Func<float, float, float, float, Vector4>)NewVec4;

            LoadFile(scriptLocator.OpenFileStream("scripts/lib/standard.lua"));
            var converters = LoadFile(scriptLocator.OpenFileStream("scripts/lib/vectors.lua"));

            m_luaConverters[typeof(Vector2)] = (DynValue)converters.Tuple.GetValue(0);
            m_luaConverters[typeof(Vector3)] = (DynValue)converters.Tuple.GetValue(1);
            m_luaConverters[typeof(Vector4)] = (DynValue)converters.Tuple.GetValue(2);
        }

        public void InitSpriteRenderer(Vector2? viewportSize = null)
        {
            m_renderer = new BasicSpriteRenderer(ResourceLocator, viewportSize);
            this["g2d"] = m_renderer;
        }

        public bool LuaAsyncLoad()
        {
            var result = CallIfExists("AsyncLoad");
            if (result == null)
                return true;

            if (!result.CastToBool())
                return false;

            if (!Resources.LoadAll())
                return false;

            return true;
        }

        public bool LuaAsyncFinalize()
        {
            var result = CallIfExists("AsyncFinalize");
            if (result == null)
                return true;

            if (!result.CastToBool())
                return false;

            if (!Resources.FinalizeLoad())
                return false;

            return true;
        }

        public void Update(float delta, float total)
        {
            CallIfExists("Update", delta, total);
        }

        public void Draw()
        {
            if (m_renderer != null)
            {
                m_renderer.BeginFrame();
                CallIfExists("Draw");
                m_renderer.Flush();
                m_renderer.EndFrame();
            }
        }

        public DynValue DoString(string code, string? codeFriendlyName = null) => Script.DoString(code, codeFriendlyName: codeFriendlyName);

        /// <summary>
        /// Takes ownership of the file stream.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <returns></returns>
        public DynValue LoadFile(Stream fileStream, string? codeFriendlyName = null)
        {
            using (var reader = new StreamReader(fileStream))
            {
                string code = reader.ReadToEnd();
                return DoString(code, codeFriendlyName);
            }
        }

        public async Task<DynValue> LoadFileAsync(Stream fileStream)
        {
            using (var reader = new StreamReader(fileStream))
            {
                string code = await reader.ReadToEndAsync();
                return DoString(code);
            }
        }

        public DynValue Call(string name, params object[] args)
        {
            return Script.Call(this[name], args);
        }

        public DynValue? CallIfExists(string name, params object[] args)
        {
            var target = this[name];
            if (target is Closure || target is CallbackFunction)
                return Script.Call(target, args);
            else return null;
        }

        public DynValue Call(object val, params object[] args)
        {
            return Script.Call(val, args);
        }

#region New

        public Table NewTable() => new Table(Script);
        public ScriptEvent NewEvent() => new ScriptEvent(this);

#endregion
    }
}
