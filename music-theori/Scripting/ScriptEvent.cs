using System.Collections.Generic;
using MoonSharp.Interpreter;

namespace theori.Scripting
{
    public sealed class ScriptEvent
    {
        public sealed class Connection
        {
            private readonly ScriptEvent m_event;
            private readonly DynValue m_value;

            internal Connection(ScriptEvent scriptEvent, DynValue value)
            {
                m_event = scriptEvent;
                m_value = value;
            }

            public void Disconnect()
            {
                m_event.m_connected.Remove(m_value);
            }
        }

        private readonly LuaScript m_script;
        private readonly List<DynValue> m_connected = new List<DynValue>();

        internal ScriptEvent(LuaScript script)
        {
            m_script = script;
        }

        public void Destroy()
        {
            m_connected.Clear();
        }

        [MoonSharpHidden]
        public void Fire(params object[] args)
        {
            foreach (var c in m_connected)
                m_script.Call(c, args);
        }

        public Connection Connect(DynValue value)
        {
            m_connected.Add(value);
            return new Connection(this, value);
        }
    }
}
