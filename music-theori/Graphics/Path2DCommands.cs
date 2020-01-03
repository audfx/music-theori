using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MoonSharp.Interpreter;
using theori;

namespace theori.Graphics
{
    public enum Path2DCommandKind
    {
        MoveTo, LineTo, CubicBezierTo, QuadraticBezierTo, Close, Winding,
    }

    [Flags]
    public enum Path2DPointFlags
    {
        None = 0,
        Corner = 1, Left = 2, Bevel = 4, InnerBevel = 8,
    }

    public enum Path2DLineCap
    {
        Butt, Round, Sqaure, Bevel, Miter,
    };

    public struct Path2DCommand : IEnumerable<Vector2>
    {
        public Path2DCommandKind Command;
        public AngularDirection WindingArgument;

        public Vector2[] Points;

        public Path2DCommand(Path2DCommandKind cmd, params Vector2[] points)
        {
            Command = cmd;
            WindingArgument = 0;
            Points = points;
        }

        public Path2DCommand(Path2DCommandKind cmd, AngularDirection argument)
        {
            Command = cmd;
            WindingArgument = argument;
            Points = new Vector2[0];
        }

        public IEnumerator<Vector2> GetEnumerator() => ((IEnumerable<Vector2>)Points).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Vector2>)Points).GetEnumerator();
    }

    public struct ScreenSpacePointData
    {
        public Vector2 Position;
        public Vector2 Direction, DM;
        public float Length;
        public Vector2 RelativeTextureCoord;
        public Path2DPointFlags Flags;

        public ScreenSpacePointData(Vector2 pos, Vector2? texCoord)
        {
            Position = pos;
            RelativeTextureCoord = texCoord ?? pos;

            Direction = Vector2.One;
            DM = Vector2.Zero;
            Length = 0;

            Flags = Path2DPointFlags.Corner;
        }
    }


    public struct Path2D
    {
        public const float TesselationTolerance = 0.25f;

        public ScreenSpacePointData[] Points;
        public AngularDirection Winding;
        public bool IsClosed;
        public bool IsConvex;
        public int Bevel;

        public Path2D(ScreenSpacePointData[] points, AngularDirection winding, bool isClosed)
        {
            Points = points;
            Winding = winding;
            IsClosed = isClosed;
            IsConvex = true;
            Bevel = 0;
        }

        public bool IsClockwise()
        {
            float sum = 0;
            for (int i = 1; i < Points.Length; i++)
            {
                sum += (Points[i].Position.X - Points[i - 1].Position.X) *
                       (Points[i].Position.Y + Points[i - 1].Position.Y);
            }
            sum += (Points[0].Position.X - Points[^1].Position.X) *
                   (Points[0].Position.Y + Points[^1].Position.Y);
            return sum < 0;
        }
    }

    public class Path2DGroup
    {
        static float MaxValue<T>(IEnumerable<T> e, Func<T, float> f) => e.Aggregate(float.MinValue, (x, v) => MathL.Max(x, f(v)));
        static float MinValue<T>(IEnumerable<T> e, Func<T, float> f) => e.Aggregate(float.MaxValue, (x, v) => MathL.Min(x, f(v)));

        public Path2D[] Paths { get; }

        public float MinX => MinValue(Paths, p => MinValue(p.Points, x => x.Position.X));
        public float MinY => MinValue(Paths, p => MinValue(p.Points, x => x.Position.Y));

        public float MaxX => MaxValue(Paths, p => MaxValue(p.Points, x => x.Position.X));
        public float MaxY => MaxValue(Paths, p => MaxValue(p.Points, x => x.Position.Y));

        public float SpanWidth => MaxX - MinX;
        public float SpanHeight => MaxY - MinY;

        public Path2DGroup(Path2D[] paths)
        {
            Paths = paths;
        }

        public Path2DGroup Expand(float amt)
        {
            var resultPaths = new List<Path2D>();

            foreach (var path in Paths)
            {
                // 1. Break the path into line segments expanded by the desired amount

                var expandedLines = new List<(Vector2 Start, Vector2 End)>();
                for (int i = 0, len = path.Points.Length; i < len; i++)
                {
                    if (!path.IsClosed && i == len - 1)
                        break;

                    int j = (i + 1) % len;

                    Vector2 p0 = path.Points[i].Position, p1 = path.Points[j].Position;

                    Vector2 dir = p1 - p0; // make sure, in case length and dir aren't set
                    float length = dir.Length();

                    Vector2 normal = Vector2.Normalize(new Vector2(dir.Y, -dir.X));
                    expandedLines.Add((p0 + normal * amt, p1 + normal * amt));
                }

                // 2. For each of those lines, join them by either creating a new line between them
                //    or by extending them to meet.

                for (int i = 0; i < expandedLines.Count; i++)
                {
                    if (!path.IsClosed && i == expandedLines.Count - 1)
                        break;

                    int j = (i + 1) % expandedLines.Count;

                    var (l0Start, l0End) = expandedLines[i];
                    var (l1Start, l1End) = expandedLines[j];

                    static Vector2 InfLineIntersection(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
                    {
                        float a0 = p1.Y - p0.Y,
                              b0 = p0.X - p1.X,
                              c0 = a0 * p0.X + b0 * p0.Y,
                              a1 = p3.Y - p2.Y,
                              b1 = p2.X - p3.X,
                              c1 = a1 * p2.X + b1 * p2.Y,
                              dn = a0 * b1 - a1 * b0;

                        return new Vector2((b1 * c0 - b0 * c1) / dn, (a0 * c1 - a1 * c0) / dn);
                    }

                    // TODO(local): actuall figure out different kinds of expansion and where to flag them
                    bool intersect = !path.Points[i].Flags.HasFlag(Path2DPointFlags.Bevel);
                    if (intersect)
                    {
                        l0End = l1Start = InfLineIntersection(l0Start, l0End, l1Start, l1End);

                        expandedLines[i] = (l0Start, l0End);
                        expandedLines[j] = (l1Start, l1End);
                    }
                    else
                    {
                        if (SegmentIntersects(l0Start, l0End, l1Start, l1End) is Vector2 point)
                        {
                            l0End = l1Start = point;

                            expandedLines[i] = (l0Start, l0End);
                            expandedLines[j] = (l1Start, l1End);
                        }
                        else expandedLines.Insert(++i, (l0End, l1Start));

                        static bool PointOnLine(Vector2 p, Vector2 q, Vector2 r)
                        {
                            const float EPSILON = 0.001f;
                            return q.X <= Math.Max(p.X, r.X) + EPSILON && q.X >= Math.Min(p.X, r.X) - EPSILON &&
                                   q.Y <= Math.Max(p.Y, r.Y) + EPSILON && q.Y >= Math.Min(p.Y, r.Y) - EPSILON;
                        }

                        static Vector2? SegmentIntersects(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
                        {
                            var point = InfLineIntersection(p0, p1, p2, p3);
                            if (PointOnLine(p0, point, p1) && PointOnLine(p2, point, p3))
                                return point;
                            return null;
                        }
                    }
                }

                // 3. For each of the newly expanded and joined sections, create a new list of points.
                
                var points = expandedLines.Select(pair => new ScreenSpacePointData(pair.Start, null)).ToArray();

                // 4. Add the new path to our results with the same settings as the parent path.

                resultPaths.Add(new Path2D(points, path.Winding, path.IsClosed));
            }

            return new Path2DGroup(resultPaths.ToArray());
        }

        public Path2DGroup Stroke(float w)
        {
            var resultPaths = new List<Path2D>();

            foreach (var path in Paths)
            {
            }

            return new Path2DGroup(resultPaths.ToArray());
        }
    }

    public sealed class Path2DCommands : IEnumerable<Path2DCommand>
    {
        static Vector2[] FloatsToVectors(float[] args)
        {
            if (args.Length % 2 != 0)
                throw new ArgumentException("Can only convert floats to Vector2s in pairs.");

            var result = new Vector2[args.Length / 2];
            for (int i = 0; i < args.Length; i += 2)
                result[i / 2] = new Vector2(args[i], args[i + 1]);
            return result;
        }

        private readonly List<Path2DCommand> m_commands = new List<Path2DCommand>();

        private void AddCommand(Path2DCommandKind cmd, params float[] args) =>
            m_commands.Add(new Path2DCommand(cmd, FloatsToVectors(args)));

        private void AddCommand(Path2DCommandKind cmd, params Vector2[] args) =>
            m_commands.Add(new Path2DCommand(cmd, args));

        public void MoveTo(float x, float y) => MoveTo(new Vector2(x, y));
        [MoonSharpHidden]
        public void MoveTo(Vector2 p) => AddCommand(Path2DCommandKind.MoveTo, p);

        public void LineTo(float x, float y) => LineTo(new Vector2(x, y));
        [MoonSharpHidden]
        public void LineTo(Vector2 p) => AddCommand(Path2DCommandKind.LineTo, p);

        public void CubicBezierTo(float cx0, float cy0, float cx1, float cy1, float x, float y) => AddCommand(Path2DCommandKind.CubicBezierTo, cx0, cy0, cx1, cy1, x, y);
        [MoonSharpHidden]
        public void CubicBezierTo(Vector2 cp0, Vector2 cp1, Vector2 p) => AddCommand(Path2DCommandKind.CubicBezierTo, cp0, cp1, p);

        public void QuadraticBezierTo(float cx0, float cy0, float x, float y) => AddCommand(Path2DCommandKind.QuadraticBezierTo, cx0, cy0, x, y);
        [MoonSharpHidden]
        public void QuadraticBezierTo(Vector2 cp0, Vector2 p) => AddCommand(Path2DCommandKind.QuadraticBezierTo, cp0, p);

        //public void Winding(float dir) => m_commands.Add(new Path2DCommand(Path2DCommandKind.Winding, dir));
        [MoonSharpHidden]
        public void Winding(AngularDirection dir) => m_commands.Add(new Path2DCommand(Path2DCommandKind.Winding, dir));

        public void Close() => m_commands.Add(new Path2DCommand(Path2DCommandKind.Close));

        /* x = (1 - t) * (1 - t) * p[0].x + 2 * (1 - t) * t * p[1].x + t * t * p[2].x;
         * y = (1 - t) * (1 - t) * p[0].y + 2 * (1 - t) * t * p[1].y + t * t * p[2].y;
         */

        private void CubicBezierToPoints(List<ScreenSpacePointData> toData,
            Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3,
           int level, int type)
        {
            float x01, y01, x12, y12, x23, y23, x012, y012, x123, y123, x0123, y0123;
            float dx, dy, d2, d3;

            if (level > 10) return;

            x01 = (v0.X + v1.X) * 0.5f;
            y01 = (v0.Y + v1.Y) * 0.5f;
            x12 = (v1.X + v2.X) * 0.5f;
            y12 = (v1.Y + v2.Y) * 0.5f;
            x23 = (v2.X + v3.X) * 0.5f;
            y23 = (v2.Y + v3.Y) * 0.5f;
            x012 = (x01 + x12) * 0.5f;
            y012 = (y01 + y12) * 0.5f;

            dx = v3.X - v0.X;
            dy = v3.Y - v0.Y;
            d2 = MathL.Abs((v1.X - v3.X) * dy - (v1.Y - v3.Y) * dx);
            d3 = MathL.Abs((v2.X - v3.X) * dy - (v2.Y - v3.Y) * dx);

            if ((d2 + d3) * (d2 + d3) < Path2D.TesselationTolerance * (dx * dx + dy * dy))
            {
                toData.Add(new ScreenSpacePointData(v3, null));
                return;
            }

            x123 = (x12 + x23) * 0.5f;
            y123 = (y12 + y23) * 0.5f;
            x0123 = (x012 + x123) * 0.5f;
            y0123 = (y012 + y123) * 0.5f;

            CubicBezierToPoints(toData, v0, new Vector2(x01, y01), new Vector2(x012, y012), new Vector2(x0123, y0123), level + 1, 0);
            CubicBezierToPoints(toData, new Vector2(x0123, y0123), new Vector2(x123, y123), new Vector2(x23, y23), v3, level + 1, type);
        }

        private void QuadradicBezierToPoints(List<ScreenSpacePointData> toData,
            Vector2 v0, Vector2 v1, Vector2 v2,
           int level, int type)
        {
            float x01, y01, x12, y12, x012, y012;
            float dx, dy, d2, d3;

            if (level > 10) return;

            x01 = (v0.X + v1.X) * 0.5f;
            y01 = (v0.Y + v1.Y) * 0.5f;
            x12 = (v1.X + v2.X) * 0.5f;
            y12 = (v1.Y + v2.Y) * 0.5f;
            x012 = (x01 + x12) * 0.5f;
            y012 = (y01 + y12) * 0.5f;

            dx = v2.X - v0.X;
            dy = v2.Y - v0.Y;
            d2 = MathL.Abs((v1.X - v2.X) * dy - (v1.Y - v2.Y) * dx);
            d3 = MathL.Abs((v2.X - v2.X) * dy - (v2.Y - v2.Y) * dx);

            if ((d2 + d3) * (d2 + d3) < Path2D.TesselationTolerance * (dx * dx + dy * dy))
            {
                toData.Add(new ScreenSpacePointData(v2, null));
                return;
            }

            QuadradicBezierToPoints(toData, v0, new Vector2(x01, y01), new Vector2(x012, y012), level + 1, 0);
            QuadradicBezierToPoints(toData, new Vector2(x012, y012), new Vector2(x12, y12), v2, level + 1, type);
        }

        [MoonSharpHidden]
        public Path2DGroup Flatten(float scale = 1.0f)
        {
            var result = new List<Path2D>();

            var data = new List<ScreenSpacePointData>();
            AngularDirection winding = AngularDirection.Clockwise;
            bool isClosed = false;

            Vector2 minComponent = new Vector2(float.MaxValue);
            Vector2 maxComponent = new Vector2(float.MinValue);

            void SetMinMax(Vector2 point)
            {
                minComponent.X = MathL.Min(minComponent.X, point.X);
                minComponent.Y = MathL.Min(minComponent.Y, point.Y);
                maxComponent.X = MathL.Max(maxComponent.X, point.X);
                maxComponent.Y = MathL.Max(maxComponent.Y, point.Y);
            }

            void FinishPath()
            {
                if (data.Count > 0)
                {
                    result.Add(new Path2D(data.ToArray(), winding, isClosed));
                    data.Clear();
                }
            }

            foreach (var cmd in m_commands)
            {
                switch (cmd.Command)
                {
                    case Path2DCommandKind.MoveTo:
                    {
                        FinishPath();

                        var point = cmd.Points[0];
                        SetMinMax(cmd.Points[0]);

                        data.Add(new ScreenSpacePointData(point, cmd.Points[0]));
                    } break;

                    case Path2DCommandKind.LineTo:
                    {
                        var point = cmd.Points[0];
                        SetMinMax(cmd.Points[0]);

                        data.Add(new ScreenSpacePointData(point, cmd.Points[0]));
                    } break;

                    case Path2DCommandKind.QuadraticBezierTo:
                    {
                        int dataIndex = data.Count;
                        QuadradicBezierToPoints(data, data[^1].Position, cmd.Points[0], cmd.Points[1], 0, 0);

                        for (int i = dataIndex; i < data.Count; i++)
                            SetMinMax(data[i].Position);
                    } break;

                    case Path2DCommandKind.CubicBezierTo:
                    {
                        int dataIndex = data.Count;
                        CubicBezierToPoints(data, data[^1].Position, cmd.Points[0], cmd.Points[1], cmd.Points[2], 0, 0);

                        for (int i = dataIndex; i < data.Count; i++)
                            SetMinMax(data[i].Position);
                    } break;

                    case Path2DCommandKind.Close: isClosed = true; break;
                    case Path2DCommandKind.Winding: winding = cmd.WindingArgument; break;
                }
            }

            FinishPath();

            var paths = result.ToArray();
            for (int i = 0; i < paths.Length; i++)
            {
                ref var path = ref paths[i];

                if (path.Points[0].Position == path.Points[^1].Position)
                {
                    path.IsClosed = true;
                    var points = path.Points;
                    path.Points = new ScreenSpacePointData[points.Length - 1];
                    Array.Copy(points, 0, path.Points, 0, points.Length - 1);
                }

                if (path.Points.Length > 2)
                {
                    bool isClockwise = path.IsClockwise();
                    if ((isClockwise && path.Winding != AngularDirection.Clockwise) ||
                        (!isClockwise && path.Winding != AngularDirection.CounterClockwise))
                    {
                        Array.Reverse(path.Points);
                    }
                }

                for (int j = 0; j < path.Points.Length; j++)
                {
                    ref var d = ref path.Points[j];
                    
                    d.RelativeTextureCoord = (d.RelativeTextureCoord - minComponent) / (maxComponent - minComponent);

                    int k = (j + 1) % path.Points.Length;
                    var dir = path.Points[k].Position - d.Position;

                    d.Direction = Vector2.Normalize(dir);
                    d.Length = dir.Length();

                    d.Position *= scale;
                }
            }

            return new Path2DGroup(paths);
        }

        [MoonSharpHidden]
        public IEnumerator<Path2DCommand> GetEnumerator() => ((IEnumerable<Path2DCommand>)m_commands).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Path2DCommand>)m_commands).GetEnumerator();
    }
}
