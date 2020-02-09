using MoonSharp.Interpreter;

namespace theori.Scripting
{
    public abstract class LuaInstance
    {
        static LuaInstance()
        {
            ScriptService.RegisterType<LuaInstance>();
        }

        public readonly ExecutionEnvironment ExecutionEnvironment;
        public Script L => ExecutionEnvironment.L;

        protected LuaInstance(ExecutionEnvironment env)
        {
            ExecutionEnvironment = env;
        }
    }

    public sealed class LuaInstanceNamespace : LuaInstance
    {
        static LuaInstanceNamespace()
        {
            ScriptService.RegisterType<LuaInstanceNamespace>();
        }

        public LuaInstanceNamespace(ExecutionEnvironment env)
            : base(env)
        {
        }

        public Layer NewLayer() => new Layer(ExecutionEnvironment);
    }
}
