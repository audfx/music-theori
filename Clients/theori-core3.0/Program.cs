using System;

using theori.Platform.Windows;

namespace theori.Core30
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            TestLoading(args);
        }

        static void TestLoading(string[] args)
        {
            Host.Platform = new WindowsPlatform();

            Host.Platform.LoadLibrary("x64/SDL2.dll");

            Host.DefaultInitialize();
            Host.StartShared(args);
        }
    }
}
