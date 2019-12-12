using System;
using System.Collections.Generic;

using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace theori.Charting
{
    public sealed class ControlPoint : ILinkable<ControlPoint>, IComparable<ControlPoint>, ICloneable
    {
        private tick_t m_position;
        private time_t m_calcPosition = (time_t)long.MinValue;

        private double m_bpm = 120;
        private int m_beatCount = 4, m_beatKind = 4;

        [TheoriProperty("position")]
        [MoonSharpVisible(true)]
        public tick_t Position
        {
            get => m_position;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Control points cannot have negative positions.", nameof(Position));

                m_position = value;

                Chart?.InvalidateTimeCalc();
                Chart?.ControlPoints.Resort();
            }
        }

        /// <summary>
        /// Time Signature Numerator
        /// </summary>
        [TheoriProperty("numerator")]
        [MoonSharpVisible(true)]
        public int BeatCount
        {
            get => m_beatCount;
            set
            {
                if (value == m_beatCount)
                    return;
                m_beatCount = value;
                Chart?.InvalidateTimeCalc();
            }
        }

        /// <summary>
        /// Time Signature Denominator
        /// </summary>
        [TheoriProperty("denominator")]
        [MoonSharpVisible(true)]
        public int BeatKind
        {
            get => m_beatKind;
            set
            {
                if (value == m_beatKind)
                    return;
                m_beatKind = value;
                Chart?.InvalidateTimeCalc();
            }
        }

        [TheoriProperty("multiplier")]
        [MoonSharpVisible(true)]
        public double SpeedMultiplier { get; set; } = 1.0;

        [MoonSharpVisible(true)]
        public time_t AbsolutePosition
        {
            get
            {
                if (Chart == null)
                    throw new InvalidOperationException("Cannot calculate the absolute position of a control point without an assigned Chart.");

                if (m_calcPosition == (time_t)long.MinValue)
                {
                    var prev = Previous;
                    if (prev == null)
                        m_calcPosition = Chart.Offset;
                    else
                    {
                        m_calcPosition = prev.AbsolutePosition
                                       + prev.MeasureDuration * (m_position - prev.m_position);
                    }
                }

                return m_calcPosition;
            }
        }

        /// <summary>
        /// The number of quarter-note beats per minute.
        /// </summary>
        [TheoriProperty("bpm")]
        [MoonSharpVisible(true)]
        public double BeatsPerMinute
        {
            get => m_bpm;
            set
            {
                if (value <= 0.0)
                    throw new ArgumentException(nameof(BeatsPerMinute), "BPM must be a positive value.");

                m_bpm = value;
                QuarterNoteDuration = time_t.FromSeconds(60.0 / value);

                Chart?.InvalidateTimeCalc();
            }
        }

        [MoonSharpVisible(true)]
        public time_t SectionDuration
        {
            get
            {
                if (HasNext) return Next.AbsolutePosition - AbsolutePosition;
                else return Chart.TimeEnd - AbsolutePosition;
            }
        }

        [MoonSharpVisible(true)]
        public time_t QuarterNoteDuration { get; private set; } = time_t.FromSeconds(60.0 / 120);
        [MoonSharpVisible(true)]
        public time_t BeatDuration => QuarterNoteDuration * 4 / BeatKind;

        [MoonSharpVisible(true)]
        public time_t MeasureDuration => BeatDuration * BeatCount;

        [TheoriProperty("stop")]
        [MoonSharpVisible(true)]
        public bool StopChart { get; set; } = false;

        [MoonSharpVisible(true)]
        public bool HasPrevious => Previous != null;
        [MoonSharpVisible(true)]
        public bool HasNext => Next != null;

        [MoonSharpVisible(true)]
        public ControlPoint Previous => ((ILinkable<ControlPoint>)this).Previous;
        ControlPoint ILinkable<ControlPoint>.Previous { get; set; }

        [MoonSharpVisible(true)]
        public ControlPoint Next => ((ILinkable<ControlPoint>)this).Next;
        ControlPoint ILinkable<ControlPoint>.Next { get; set; }

        [TheoriIgnore]
        [MoonSharpVisible(true)]
        public Chart Chart { get; internal set; }

        public ControlPoint()
        {
        }

        [MoonSharpHidden]
        public ControlPoint Clone()
        {
            var result = new ControlPoint()
            {
                m_position = m_position,
                m_bpm = m_bpm,
                m_beatCount = m_beatCount,
                m_beatKind = m_beatKind,

                QuarterNoteDuration = QuarterNoteDuration,
                StopChart = StopChart,
            };
            return result;
        }

        object ICloneable.Clone() => Clone();

        int IComparable<ControlPoint>.CompareTo(ControlPoint other) => m_position.CompareTo(other.m_position);

        internal void InvalidateCalc()
        {
            m_calcPosition = (time_t)long.MinValue;
        }
    }
}
