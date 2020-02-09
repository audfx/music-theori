using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace theori.Scripting
{
    public sealed class UserService : LuaService, IUserDataType
    {
        static UserService()
        {
            ScriptService.RegisterType<UserService>();
        }

        [MoonSharpHidden]
        public readonly Table ServiceTable;

        public readonly string Name;

        public UserService(ExecutionEnvironment env, string serviceName, Table serviceTable)
            : base(env)
        {
            Name = serviceName;
            ServiceTable = serviceTable;
        }

        DynValue IUserDataType.Index(Script script, DynValue index, bool isDirectIndexing)
        {
            return ServiceTable.Get(index);
        }

        bool IUserDataType.SetIndex(Script script, DynValue index, DynValue value, bool isDirectIndexing)
        {
            throw new ScriptRuntimeException($"User services are readonly types; cannot assign to {Name}.");
        }

        DynValue IUserDataType.MetaIndex(Script script, string metaname)
        {
            throw new ScriptRuntimeException($"Cannot meta index a user service.");
        }
    }
}
