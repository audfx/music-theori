using MoonSharp.Interpreter;

namespace theori.Scripting
{
    public abstract class LuaService : LuaInstance
    {
        protected LuaService(ExecutionEnvironment env)
            : base(env)
        {
        }
    }
}
