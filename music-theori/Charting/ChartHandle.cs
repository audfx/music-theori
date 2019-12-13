using System.Collections;
using System.Linq;

using MoonSharp.Interpreter;
using theori.Database;
using theori.Resources;
using theori.Scripting;

namespace theori.Charting
{
    public sealed class ChartHandle : BaseScriptInstance
    {
        [MoonSharpHidden]
        public readonly Chart Chart;
        [MoonSharpHidden]
        public readonly ClientResourceManager Resources;
        [MoonSharpHidden]
        public readonly ScriptProgram Script;
        [MoonSharpHidden]
        public readonly ChartDatabaseWorker Worker;

        public ChartSetInfoHandle SetInfo => new ChartSetInfoHandle(Resources, Script, Worker, Chart.SetInfo);

        public IEnumerable ControlPoints => Chart.ControlPoints;
        public IEnumerable Lanes => Chart.Lanes.Select(lane => lane.Label);

        public ChartHandle(ClientResourceManager resources, ScriptProgram script, ChartDatabaseWorker worker, Chart chart)
        {
            Resources = resources;
            Script = script;
            Worker = worker;
            Chart = chart;
        }
    }
}
