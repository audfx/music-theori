using System;
using System.Collections.Generic;
using System.IO;

namespace theori
{
    public enum EventPhase
    {
        Duration = 'X',
        Complete = 'X',

        DurationBegin = 'B',
        DurationEnd = 'E',
    }

    public class ProfilerToken
    {
        public string Name;
        public string Category;
        public EventPhase Phase;
        public long TimeStamp;
        public long? ThreadTimeStamp;
        public int ProcessId;
        public int ThreadId;
        public string? ColorName;

        public readonly Dictionary<string, string> Arguments = new Dictionary<string, string>();

        public long? Duration;

        public ProfilerToken(string name, string cat, EventPhase phase, long timeStamp, int processId, int threadId)
        {
            Name = name;
            Category = cat;
            Phase = phase;
            TimeStamp = timeStamp;
            ProcessId = processId;
            ThreadId = threadId;
        }

        public ProfilerToken AddArgument(string key, string value)
        {
            Arguments[key] = value;
            return this;
        }

        public ProfilerToken AddArguments(params (string Key, string Value)[] args)
        {
            foreach (var (key, value) in args)
                Arguments[key] = value;
            return this;
        }

        public ProfilerToken AddArguments(params string[] args)
        {
            if (args.Length % 2 != 0) throw new ArgumentException("Args must be passed in groups of two");
            for (int i = 0; i < args.Length; i += 2)
                Arguments[args[i + 0]] = args[i + 1];
            return this;
        }
    }

    public static class Profiler
    {
        public static bool IsEnabled { get; set; } =
#if DEBUG
            true;
#else
            false;
#endif

        class ProfilerScope : IDisposable
        {
            public readonly string ScopeName;

            public ProfilerScope(string scopeName)
            {
                ScopeName = scopeName;
                EmitBegin(this);
            }

            void IDisposable.Dispose()
            {
                EmitEnd(this);
            }
        }

        // TODO(local): allow listening to session events, where json writing is then just a listener
        class Session
        {
            public readonly string Name;

            private StreamWriter? m_writer;
            private bool m_hasBeenWritten = false;

            public Session(string sessionName)
            {
                Name = sessionName;

                if (!IsEnabled) return;

                m_writer = new StreamWriter(File.Open($"./NeuroSonicProfiler-{sessionName}.json", FileMode.Create, FileAccess.Write));
                m_writer.Write('[');
            }

            public void Finish()
            {
                if (!IsEnabled) return;

                m_writer!.Write(']');
                m_writer!.Flush();

                m_writer!.Dispose();
                m_writer = null;
            }

            private void CheckComma()
            {
                if (!m_hasBeenWritten)
                {
                    m_hasBeenWritten = true;
                    return;
                }
                m_writer!.Write(',');
            }

            internal void EmitBegin(ProfilerScope scope)
            {
                if (!IsEnabled) return;

                long when = CurrentTime();

                CheckComma();
                m_writer!.Write($"{{\"name\":\"{scope.ScopeName}\",\"cat\":\"{Name}\",\"ph\":\"B\",\"ts\":{when},\"pid\":0,\"tid\":0}}");
            }

            internal void EmitEnd(ProfilerScope scope)
            {
                if (!IsEnabled) return;

                long when = CurrentTime();

                CheckComma();
                m_writer!.Write($"{{\"ph\":\"E\",\"ts\":{when},\"pid\":0,\"tid\":0}}");
            }

            internal void EmitDuration(ProfilerScope scope, long startTime)
            {
                if (!IsEnabled) return;

                long when = CurrentTime();

                CheckComma();
                m_writer!.Write($"{{\"name\":\"{scope.ScopeName}\",\"cat\":\"{Name}\",\"ph\":\"X\",\"ts\":{startTime},\"dur\":{when-startTime},\"pid\":0,\"tid\":0}}");
            }

            internal void EmitInstant(string instantName, string scope = "p")
            {
                if (!IsEnabled) return;

                long when = CurrentTime();

                CheckComma();
                m_writer!.Write($"{{\"name\":\"{instantName}\",\"cat\":\"{Name}\",\"ph\":\"i\",\"ts\":{when},\"pid\":0,\"tid\":0,\"s\":\"{scope}\"}}");
            }
        }

        private static Session? currentSession;

        public static void BeginSession(string sessionName)
        {
            if (currentSession != null) throw new InvalidOperationException($"Cannot start a profiling session while one is already running! (\"{ currentSession!.Name }\")");

            currentSession = new Session(sessionName);
        }

        public static void EndSession()
        {
            if (currentSession == null) throw new InvalidOperationException("Cannot end a profiling session if no session has been started.");

            currentSession.Finish();
            currentSession = null;
        }

        public static IDisposable? Scope(string scopeName)
        {
            if (!IsEnabled || currentSession == null) return null;
            return new ProfilerScope(scopeName);
        }

        static long CurrentTime() => Time.HighResolution;

        static void EmitBegin(ProfilerScope scope)
        {
            if (currentSession != null)
                currentSession!.EmitBegin(scope);
        }

        static void EmitEnd(ProfilerScope scope)
        {
            if (currentSession != null)
                currentSession!.EmitEnd(scope);
        }

        static void EmitDuration(ProfilerScope scope, long startTime)
        {
            if (currentSession != null)
                currentSession!.EmitDuration(scope, startTime);
        }

        public static void Instant(string instantName, string scope = "p", params (string, string)[] args)
        {
            if (currentSession != null)
                currentSession!.EmitInstant(instantName, scope);
        }
    }
}
