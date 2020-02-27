using System;
using System.Collections.Generic;
using System.Text;

using LuaState = MoonSharp.Interpreter.Script;

namespace theori
{
    public class ExecutionEnvironment
    {
        private static ExecutionEnvironment? m_instance = null;
        public static ExecutionEnvironment Instance => m_instance ?? throw new InvalidOperationException("The :theori execution engine hasn't been initialized yet.");

        internal static void Initialize()
        {
            m_instance = new ExecutionEnvironment();
        }

        private readonly LuaState m_luaState;

        internal ExecutionEnvironment()
        {
            m_luaState = new LuaState();
        }
    }
}
