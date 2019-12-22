using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using MoonSharp.Interpreter;
using theori;

namespace theori.Graphics
{
    public enum Path2DCommandKind
    {
        MoveTo, LineTo, BezierTo, Close, Winding,
    }

    [Flags]
    public enum Path2DPointFlags
    {
        Corner = 1, Left = 2, Bevel = 4, InnerBevel = 8,
    }

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

    public sealed class Path2DCommands : IEnumerable<Path2DCommand>
    {
        private readonly List<Path2DCommand> m_commands = new List<Path2DCommand>();

        static Vector2[] FloatsToVectors(float[] args)
        {
            if (args.Length % 2 != 0)
                throw new ArgumentException("Can only convert floats to Vector2s in pairs.");

            var result = new Vector2[args.Length / 2];
            for (int i = 0; i < args.Length; i += 2)
                result[i / 2] = new Vector2(args[i], args[i + 1]);
            return result;
        }

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

        public void BezierTo(float cx0, float cy0, float cx1, float cy1, float x, float y) => AddCommand(Path2DCommandKind.BezierTo, cx0, cy0, cx1, cy1, x, y);
        [MoonSharpHidden]
        public void BezierTo(Vector2 cp0, Vector2 cp1, Vector2 p) => AddCommand(Path2DCommandKind.BezierTo, cp0, cp1, p);

        //public void Winding(float dir) => m_commands.Add(new Path2DCommand(Path2DCommandKind.Winding, dir));
        [MoonSharpHidden]
        public void Winding(AngularDirection dir) => m_commands.Add(new Path2DCommand(Path2DCommandKind.Winding, dir));

        public void Close() => m_commands.Add(new Path2DCommand(Path2DCommandKind.Close));

        [MoonSharpHidden]
        public IEnumerator<Path2DCommand> GetEnumerator() => ((IEnumerable<Path2DCommand>)m_commands).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Path2DCommand>)m_commands).GetEnumerator();
    }
}
