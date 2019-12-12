using System.Collections;
using System.Linq;

using MoonSharp.Interpreter;

using theori.Scripting;

namespace theori.Charting
{
    public sealed class ChartHandle : BaseScriptInstance
    {
        [MoonSharpHidden]
        public readonly Chart Chart;

        public IEnumerable ControlPoints => Chart.ControlPoints;
        public IEnumerable Lanes => Chart.Lanes.Select(lane => lane.Label);

        public ChartHandle(Chart chart)
        {
            Chart = chart;
        }
    }
}
