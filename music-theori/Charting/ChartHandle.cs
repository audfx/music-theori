using System;
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
        public ChartInfoHandle Info => SetInfo.Charts.Where(h => h.Object == Chart.Info).Single();

        public IEnumerable ControlPoints => Chart.ControlPoints;
        public IEnumerable Lanes => Chart.Lanes.Select(lane => lane.Label);

        public time_t TimeStart => Chart.TimeStart;
        public time_t TimeEnd => Chart.TimeEnd;

        public ChartHandle(ClientResourceManager resources, ScriptProgram script, ChartDatabaseWorker worker, Chart chart)
        {
            Resources = resources;
            Script = script;
            Worker = worker;
            Chart = chart;
        }

        public tick_t CalcTickFromTime(time_t time) => Chart.CalcTickFromTime(time);
        public time_t CalcTimeFromTick(tick_t tick) => Chart.CalcTimeFromTick(tick);

        public ControlPoint MostRecentControlPointAtTime(time_t time) => Chart.ControlPoints.MostRecent(time);

        public Entity? GetEntityAtTick(HybridLabel laneLabel, tick_t tick) => Chart[laneLabel].Find(tick, true);
        public Entity? GetEntityAtTick(HybridLabel laneLabel, tick_t tick, bool includeDuration) => Chart[laneLabel].Find(tick, includeDuration);

        public void ForEachEntityInRangeTicks(HybridLabel laneLabel, tick_t startTick, tick_t endTick, DynValue function) =>
            Chart[laneLabel].ForEachInRange(startTick, endTick, true, entity => Script.Call(function, entity));
        public void ForEachEntityInRangeTicks(HybridLabel laneLabel, tick_t startTick, tick_t endTick, bool includeDuration, DynValue function) => 
            Chart[laneLabel].ForEachInRange(startTick, endTick, includeDuration, entity => Script.Call(function, entity));

        public void AddEntity(HybridLabel lane, Entity entity) => Chart[lane].Add(entity);
        public Entity AddEntity(HybridLabel lane, string entityType, tick_t position, tick_t duration)
        {
            var entity = (Entity)Activator.CreateInstance(Entity.GetEntityTypeById(entityType));
            entity.Position = position;
            entity.Duration = duration;

            AddEntity(lane, entity);
            return entity;
        }

        public void RemoveEntity(Entity entity) => Chart[entity.Lane].Remove(entity);
        public void RemoveEntityAtTick(HybridLabel lane, tick_t tick) => Chart[lane].Remove(Chart[lane].Find(tick, false));
    }
}
