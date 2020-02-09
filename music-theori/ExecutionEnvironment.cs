using System;
using System.Collections.Concurrent;

using MoonSharp.Interpreter;
using theori.Charting;
using theori.IO;
using theori.Platform;
using theori.Scripting;

namespace theori
{
    public class ExecutionEnvironment
    {
        static ExecutionEnvironment()
        {
            ScriptService.RegisterType<ExecutionEnvironment>();
        }

        internal readonly Client Client;

        internal readonly Script L;

        private readonly ConcurrentDictionary<string, LuaService> m_serviceCache = new ConcurrentDictionary<string, LuaService>();

        public ExecutionEnvironment(Client client)
        {
            Client = client;

            L = new Script(CoreModules.Basic
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

            L.Globals["KeyCode"] = typeof(KeyCode);
            L.Globals["MouseButton"] = typeof(MouseButton);
            L.Globals["Axis"] = typeof(Axis);
        }

        [MoonSharpHidden]
        public T GetService<T>()
            where T : LuaService
        {
            return (T)GetService(typeof(T).Name);
        }

        [MoonSharpHidden]
        public Table NewTable() => new Table(L);

        [MoonSharpHidden]
        public DynValue Call(DynValue callable, params object[] args) => L.Call(callable, args);

        #region Lua Functions

        public LayerStack Layers => Client.LayerStack;

        public ChartService ChartService => GetService<ChartService>();
        public InputService InputService => GetService<InputService>();
        public RunService RunService => GetService<RunService>();

        public LuaService GetService(string serviceName)
        {
            if (!m_serviceCache.TryGetValue(serviceName, out var service))
            {
                //if (m_userServices.TryGetValue(serviceName, out var userService)) return userService;

                m_serviceCache[serviceName] = service = serviceName switch
                {
                    nameof(InputService) => new InputService(this),
                    nameof(RunService) => new RunService(this),
                    nameof(ChartService) => new ChartService(this),

                    _ => throw new ArgumentException($"\"{serviceName}\" is not the name of a service."),
                };
            }

            return service;
        }

        public void Exit() => Client.Host.Exit();

        #endregion
    }
}
