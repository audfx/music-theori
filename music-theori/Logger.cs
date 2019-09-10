using System.Collections.Generic;

namespace System
{
	public enum LogPriority
	{
		Verbose,
		Debug,
		Info,
		Warn,
		Error,
		Critical,
	}

    public sealed class LogEntry
    {
        public DateTime When;
        public string Message;

        public LogPriority Priority;

        public LogEntry(DateTime when, string message, LogPriority priority)
        {
            When = when;
            Message = message;

            Priority = priority;
        }
    }

    public interface ILoggerImpl
    {
        void Log(LogEntry entry);
        void Flush() { } // Not required to be implemented
    }

    public abstract class Logger
    {
        private static readonly List<ILoggerImpl> logImpls = new List<ILoggerImpl>();

        private static bool block = false;

        public static void AddLogger(ILoggerImpl impl)
        {
            if (impl == null) return;
            logImpls.Add(impl);
        }

        private static void OnLog(LogEntry entry)
        {
            //Diagnostics.Trace.WriteLine($"{ entry.When.ToString(CultureInfo.InvariantCulture) } [{ entry.Priority }]: { entry.Message }");
            foreach (var impl in logImpls)
                impl.Log(entry);
        }

        public static void Log(object obj, LogPriority priority = LogPriority.Verbose)
        {
            Log(obj?.ToString() ?? "null", priority);
        }

        public static void Log(string message, LogPriority priority = LogPriority.Verbose)
        {
            if (block) return;

            var entry = new LogEntry(DateTime.UtcNow, message, priority);
            OnLog(entry);
        }

        public static void Block() => block = true;
        public static void Unblock() => block = false;

        internal static void Flush()
        {
            foreach (var impl in logImpls)
                impl.Flush();
        }
    }
}
