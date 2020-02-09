using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using MoonSharp.Interpreter;

namespace theori.Scripting
{
    public sealed class LuaBindableEvent
    {
        public sealed class Connection
        {
            private static int atomicCounter = 0;

            public readonly int UniqueId;

            public readonly LuaBindableEvent Bindable;
            public readonly DynValue Callback;

            public Connection(LuaBindableEvent bindable, DynValue callback)
            {
                UniqueId = Interlocked.Increment(ref atomicCounter);

                Bindable = bindable;
                Callback = callback;
            }

            public void Disconnect() => Bindable.Disconnect(this);
        }

        private sealed class HandleStorage
        {
            public DynValue Result = DynValue.Nil;
        }

        static LuaBindableEvent()
        {
            UserData.RegisterType<LuaBindableEvent>();
            UserData.RegisterType<Connection>();
        }

        public readonly Script L;

        private readonly ConcurrentDictionary<int, Connection> m_connections = new ConcurrentDictionary<int, Connection>();
        private readonly ConcurrentQueue<(EventWaitHandle Handle, HandleStorage Storage)> waitHandles = new ConcurrentQueue<(EventWaitHandle, HandleStorage)>();

        public LuaBindableEvent(Script L)
        {
            this.L = L;
        }

        [MoonSharpHidden]
        public DynValue[] Fire(params object[] args)
        {
            var result = new List<DynValue>();

            var dynValueArgs = args.Select(o => DynValue.FromObject(L, o)).ToArray();
            foreach (var (_, value) in m_connections)
                result.Add(L.Call(value.Callback, dynValueArgs));

            var tupleArgs = DynValue.NewTuple(dynValueArgs);
            while (waitHandles.TryDequeue(out var pair))
            {
                pair.Storage.Result = tupleArgs;
                pair.Handle.Set();
            }

            return result.ToArray();
        }

        [MoonSharpHidden]
        private void Disconnect(Connection con)
        {
            m_connections.TryRemove(con.UniqueId, out var _);
        }

        public Connection Connect(DynValue callable)
        {
            var con = new Connection(this, callable);
            return m_connections[con.UniqueId] = con;
        }

        public DynValue Wait()
        {
            var storage = new HandleStorage();

            var waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            waitHandles.Enqueue((waitHandle, storage));

            waitHandle.WaitOne();

            return storage.Result;
        }
    }
}
