using System.Collections.Generic;
using System.Linq;

namespace System
{
    public static class EnumExt
    {

        public static IEnumerable<T> Explode<T>(this T flags)
            where T : Enum
        {
            int flagsi = Convert.ToInt32(flags);
            return from f in Enum.GetValues(typeof(T)).Cast<T>()
                   let fi = Convert.ToInt32(f)
                   where fi != 0 && fi == (flagsi & fi) && NumberOfSetBits(fi) == 1
                   select f;

            static int NumberOfSetBits(int i)
            {
                i -= (i >> 1) & 0x55555555;
                i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
                return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
            }
        }
    }
}
