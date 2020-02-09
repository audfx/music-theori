using MoonSharp.Interpreter;

namespace theori.Scripting
{
    public sealed class RunService : LuaService
    {
        static RunService()
        {
            UserData.RegisterType<RunService>();
        }

        public readonly LuaBindableEvent Update;

        public RunService(ExecutionEnvironment env)
            : base(env)
        {
            Update = new LuaBindableEvent(L);
        }

        internal void OnUpdate(float delta, float total)
        {
            Update.Fire(delta, total);
        }
    }
}
