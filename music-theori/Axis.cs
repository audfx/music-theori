using System;

namespace theori
{
    [Flags]
    public enum Axis
    {
        None = 0,

        X = 1,
        Y = 2,

        XY = X | Y,
    }
}
