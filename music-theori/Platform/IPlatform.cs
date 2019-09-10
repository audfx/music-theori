using System;

namespace theori.Platform
{
    public interface IPlatform
    {
        IntPtr LoadLibrary(string libraryName);
        void FreeLibrary(IntPtr library);
        IntPtr GetProcAddress(IntPtr library, string procName);
    }
}
