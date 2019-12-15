using System.Collections.Generic;

namespace theori.Charting.Playback
{
    public class RandomAccessChartWindow : IChartWindow
    {
        private Chart m_chart;
        public Chart Chart
        {
            get => m_chart;
            set
            {
                if (value == m_chart) return;

                m_chart = value;
                Refresh();
            }
        }

        private time_t m_position = 0, m_lookBehind = 0, m_lookAhead = 1;

        public time_t Position
        {
            get => m_position;
            set
            {
                if (m_position == value)
                    return;
                m_position = value;
                Refresh();
            }
        }

        public time_t LookBehind
        {
            get => m_lookBehind;
            set
            {
                if (m_lookBehind == value)
                    return;
                m_lookBehind = value;
                Refresh();
            }
        }

        public time_t LookAhead
        {
            get => m_lookAhead;
            set
            {
                if (m_lookAhead == value)
                    return;
                m_lookAhead = value;
                Refresh();
            }
        }

        public event WindowEntityEvent? EntityEnter;
        public event WindowEntityEvent? EntityExit;

        private readonly Dictionary<HybridLabel, HashSet<Entity>> m_inView = new Dictionary<HybridLabel, HashSet<Entity>>();
        private readonly List<Entity> m_toRemoveCache = new List<Entity>();

        public RandomAccessChartWindow(Chart chart)
        {
            m_chart = chart;
            Refresh();
        }

        public void Refresh()
        {
            time_t startTime = Position - LookBehind;
            time_t endTime = Position + LookAhead;

            foreach (var lane in Chart.Lanes)
            {
                if (!m_inView.TryGetValue(lane.Label, out var entities))
                    entities = m_inView[lane.Label] = new HashSet<Entity>();
                m_toRemoveCache.AddRange(entities);

                lane.ForEachInRange(startTime, endTime, true, entity =>
                {
                    m_toRemoveCache.Remove(entity);
                    if (entities.Add(entity))
                        OnEntityEnter(lane.Label, entity);
                });

                foreach (var entity in m_toRemoveCache)
                {
                    entities.Remove(entity);
                    OnEntityExit(lane.Label, entity);
                }
            }
        }

        protected void OnEntityEnter(HybridLabel lane, Entity entity) => EntityEnter?.Invoke(lane, entity);
        protected void OnEntityExit(HybridLabel lane, Entity entity) => EntityExit?.Invoke(lane, entity);
    }
}
