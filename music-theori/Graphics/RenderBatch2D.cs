using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

using theori.Graphics.OpenGL;
using theori.Resources;

namespace theori.Graphics
{
    [StructLayout(LayoutKind.Sequential, Size = (1 + 2 + 2 + 4) * 4)]
    [VertexType(VertexData.Int, VertexData.Vector2, VertexData.Vector2, VertexData.Vector4)]
    public struct VertexRB2D
    {
        public int PaintIndex;
        public Vector2 Position;
        public Vector2 TexCoord;
        public Vector4 Color;

        public VertexRB2D(int paintIndex, Vector2 pos, Vector2 texCoord, Vector4 color)
        {
            PaintIndex = paintIndex;
            Position = pos;
            TexCoord = texCoord;
            Color = color;
        }
    }

    internal struct ScreenSpacePointData
    {
        public Vector2 Position;
        public Vector2 RelativeTextureCoord;

        public ScreenSpacePointData(Vector2 pos, Vector2? texCoord)
        {
            Position = pos;
            RelativeTextureCoord = texCoord ?? pos;
        }
    }

    internal struct Path2D
    {
        public ScreenSpacePointData[] Points;
        public AngularDirection Winding;
        public bool IsClosed;

        public Path2D(ScreenSpacePointData[] points, AngularDirection winding, bool isClosed)
        {
            Points = points;
            Winding = winding;
            IsClosed = isClosed;
        }
    }

    public sealed class RenderBatch2D
    {
        public readonly ClientResourceManager Resources;

        private readonly RenderBatcher2D m_batcher;
        private bool m_inUse = false;

        public RenderBatch2D(ClientResourceManager resources)
        {
            Resources = resources;
            m_batcher = new RenderBatcher2D(this);
        }

        public RenderBatcher2D Use()
        {
            if (m_inUse)
                throw new InvalidOperationException("Cannot Use a batch which is already in use.");

            m_inUse = true;

            m_batcher.Begin();
            return m_batcher;
        }

        internal void Unuse(RenderBatcher2D batcher)
        {
            if (!m_inUse)
                throw new InvalidOperationException("Cannot Unuse a batch which is not in use.");
            if (batcher != m_batcher)
                throw new InvalidOperationException("Tried to Unuse a batcher that is not associated with this batch.");
            
            m_inUse = false;
            batcher.End();
        }
    }

    enum PaintType
    {
        Image = 0,
        Gradient = 1,
    }

    internal class Paint
    {
        PaintType PaintType;
        /// <summary>0-15</summary>
        int TexId;
        Matrix3x2 PaintMatrix;
    }

    /// <summary>
    /// While it isn't quite as space efficient to do so, this system assumes you will ONLY generate complete triangles
    ///  rather than using, for example, triangle strips as a space optimization.
    /// This cannot be changed.
    /// </summary>
    public sealed class RenderBatcher2D : IDisposable
    {
        public const int MaxVertexCount = ushort.MaxValue;

        public const float Kappa90 = 0.5522847493f;
        public const float TesselationTolerance = 0.25f;

        private readonly RenderBatch2D m_batch;

        private readonly Mesh m_mesh;
        private readonly Material m_material;
        private MaterialParams m_params = new MaterialParams();

        private readonly ushort[] m_indices;
        private readonly VertexRB2D[] m_vertices;
        private int m_vertexCount = 0, m_indexCount = 0;

        private Transform m_world, m_projection, m_camera;

        private readonly List<Paint> m_paints = new List<Paint>();
        /// <summary>
        /// Textures are shared between paints and regular textured shapes.
        /// </summary>
        private readonly List<Texture> m_textures = new List<Texture>();

        internal RenderBatcher2D(RenderBatch2D parent, int vertexCount = 0)
        {
            if (vertexCount < 0 || vertexCount > MaxVertexCount)
                throw new ArgumentOutOfRangeException($"{nameof(vertexCount)} must be in the range [0,{MaxVertexCount})");
            if (vertexCount == 0)
                vertexCount = MaxVertexCount;

            m_batch = parent;

            m_mesh = new Mesh();
            m_material = parent.Resources.AquireMaterial("materials/renderBatch2D");
            m_params = new MaterialParams();

            m_indices = new ushort[vertexCount];
            m_vertices = new VertexRB2D[vertexCount];
        }

        internal void Begin()
        {
            m_vertexCount = 0;
            m_indexCount = 0;

            m_world = Transform.Identity;
            m_projection = (Transform)Matrix4x4.CreateOrthographicOffCenter(0, Window.Width, Window.Height, 0, -10, 10);
            m_camera = Transform.Identity;

            m_textures.Clear();

            m_params = new MaterialParams();
        }

        void IDisposable.Dispose() => DoEnd();
        private void DoEnd()
        {
            End();
            m_batch.Unuse(this);
        }

        internal void End()
        {
            Flush();
        }

        public void Flush()
        {
            var indices = new ushort[m_indexCount];
            var vertices = new VertexRB2D[m_vertexCount];

            Array.Copy(m_indices, 0, indices, 0, m_indexCount);
            Array.Copy(m_vertices, 0, vertices, 0, m_vertexCount);

            m_mesh.SetIndices(indices);
            m_mesh.SetVertices(vertices);

            for (int i = 0; i < 16; i++)
            {
                if (i < m_textures.Count)
                {
                    m_textures[i].Bind((uint)i);
                    m_params[$"Textures[{i}]"] = i;
                }
                else
                {
                    Texture.Unbind(TextureTarget.Texture2D, (uint)i);
                    m_params[$"Textures[{i}]"] = 0;
                }
            }

            //m_params["ViewportSize"] = new Vector2(Window.Width, Window.Height);
            //GL.Viewport(m_state.Viewport.X, -m_state.Viewport.Y, m_state.Viewport.Width, m_state.Viewport.Height);
            GL.Viewport(0, 0, Window.Width, Window.Height);

            m_material.Bind(new RenderState()
            {
                WorldTransform = m_world,
                ProjectionMatrix = m_projection,
                CameraMatrix = m_camera,

                Viewport = (0, 0, Window.Width, Window.Height),
                AspectRatio = Window.Aspect,
            }, m_params);

            GL.Enable(GL.GL_BLEND);
            GL.Enable(GL.GL_DEPTH_TEST);
            GL.DepthFunc(DepthFunction.LessThanOrEqual);
            GL.BlendFuncSeparate(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA, GL.GL_ONE, GL.GL_ONE);
            m_mesh.Draw();
            GL.Disable(GL.GL_BLEND);
            GL.Disable(GL.GL_DEPTH_TEST);

            m_indexCount = 0;
            m_vertexCount = 0;
        }

        private int AddTexture(Texture texture)
        {
            int index = m_textures.IndexOf(texture);
            if (index < 0)
            {
                if (m_textures.Count >= 16)
                    Flush();

                index = m_textures.Count;
                m_textures.Add(texture);
            }

            return index;
        }

        private void BezierToPoints(List<ScreenSpacePointData> toData,
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

            if ((d2 + d3) * (d2 + d3) < TesselationTolerance * (dx * dx + dy * dy))
            {
                toData.Add(new ScreenSpacePointData(v3, null));
                return;
            }

            x123 = (x12 + x23) * 0.5f;
            y123 = (y12 + y23) * 0.5f;
            x0123 = (x012 + x123) * 0.5f;
            y0123 = (y012 + y123) * 0.5f;

            BezierToPoints(toData, v0, new Vector2(x01, y01), new Vector2(x012, y012), new Vector2(x0123, y0123), level + 1, 0);
            BezierToPoints(toData, new Vector2(x0123, y0123), new Vector2(x123, y123), new Vector2(x23, y23), v3, level + 1, type);
        }

        private static bool IsClockwise(Path2D path)
        {
            float sum = 0;
            for (int i = 1; i < path.Points.Length; i++)
            {
                sum += (path.Points[i].Position.X - path.Points[i - 1].Position.X) *
                       (path.Points[i].Position.Y + path.Points[i - 1].Position.Y);
            }
            sum += (path.Points[0].Position.X - path.Points[^1].Position.X) *
                   (path.Points[0].Position.Y + path.Points[^1].Position.Y);
            return sum < 0;
        }

        private Path2D[] Flatten(Path2DCommands cmds, Vector2 off = default)
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
            
            foreach (var cmd in cmds)
            {
                switch (cmd.Command)
                {
                    case Path2DCommandKind.MoveTo:
                    {
                        FinishPath();

                        var point = off + cmd.Points[0];
                        SetMinMax(cmd.Points[0]);

                        data.Add(new ScreenSpacePointData(point, cmd.Points[0]));
                    } break;

                    case Path2DCommandKind.LineTo:
                    {
                        var point = off + cmd.Points[0];
                        SetMinMax(cmd.Points[0]);

                        data.Add(new ScreenSpacePointData(point, cmd.Points[0]));
                    } break;

                    case Path2DCommandKind.BezierTo:
                    {
                        int dataIndex = data.Count;
                        BezierToPoints(data, data[^1].Position, cmd.Points[0], cmd.Points[1], cmd.Points[2], 0, 0);

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
                    bool isClockwise = IsClockwise(path);
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
                    path.Points[j] = d;
                }
            }

            return paths;
        }

        private void Fill(Path2D[] paths)
        {
            // TODO(local): triangulate based on winding for carving shapes etc.
            foreach (var path in paths)
            {
                if (!path.IsClosed || path.Points.Length < 3)
                    throw new ArgumentException();

                var center = path.Points.Aggregate(new ScreenSpacePointData(Vector2.Zero, null), (a, b) => new ScreenSpacePointData(a.Position + b.Position, a.RelativeTextureCoord + b.RelativeTextureCoord));
                center.Position /= path.Points.Length;
                center.RelativeTextureCoord /= path.Points.Length;

                int vertexCount = 1 + path.Points.Length, indexCount = 3 * (vertexCount - 1);
                if (m_indexCount + indexCount >= MaxVertexCount || m_vertexCount + vertexCount >= MaxVertexCount)
                    Flush();

                int vidx = m_vertexCount;
                m_vertices[vidx + 0] = new VertexRB2D(32, center.Position, center.RelativeTextureCoord, Vector4.One);
                for (int i = 0; i < path.Points.Length; i++)
                {
                    var point = path.Points[i];
                    m_vertices[vidx + i + 1] = new VertexRB2D(32, point.Position, point.RelativeTextureCoord, Vector4.One);
                }

                int iidx = m_indexCount;
                for (int i = 0; i < vertexCount - 1; i++)
                {
                    m_indices[iidx + i * 3 + 0] = (ushort)(vidx);
                    m_indices[iidx + i * 3 + 1] = (ushort)(vidx + i + 1);

                    if (i == vertexCount - 2)
                        m_indices[iidx + i * 3 + 2] = (ushort)(vidx + 1);
                    else m_indices[iidx + i * 3 + 2] = (ushort)(vidx + i + 2);
                }

                m_vertexCount += vertexCount;
                m_indexCount += indexCount;
            }
        }

        private void Stroke(Path2D[] paths, float pos, float neg)
        {
            foreach (var path in paths)
            {
                // TODO(local): figure out corner types ig

                int vertexCount = path.Points.Length * 2;

                int vidx = m_vertexCount;
                for (int i = 0; i < path.Points.Length; i++)
                {
                    int il = i - 1 < 0 ? path.Points.Length - 1 : i - 1;
                    int ir = i + 1 >= path.Points.Length ? 0 : i + 1;

                    var l = path.Points[i].Position - path.Points[il].Position;
                    var r = path.Points[ir].Position - path.Points[i].Position;

                    var lnorm = Vector2.Normalize(new Vector2(l.Y, -l.X));
                    var rnorm = Vector2.Normalize(new Vector2(r.Y, -r.X));

                    var norm = Vector2.Normalize((lnorm + rnorm) * 0.5f);

                    var point = path.Points[i];
                    var pPos = point.Position + norm * pos;
                    var pNeg = point.Position - norm * neg;

                    // TODO(local): update relative texcoords
                    m_vertices[vidx + i * 2 + 0] = new VertexRB2D(32, pNeg, point.RelativeTextureCoord, new Vector4(1, 0, 1, 1));
                    m_vertices[vidx + i * 2 + 1] = new VertexRB2D(32, pPos, point.RelativeTextureCoord, new Vector4(1, 1, 0, 1));
                }

                int indexCount = path.Points.Length * 6;
                if (!path.IsClosed)
                    indexCount -= 6;

                int iidx = m_indexCount;
                for (int i = 0; i < path.Points.Length; i++)
                {
                    if (i == path.Points.Length - 1 && !path.IsClosed)
                        break;

                    int n0 = i * 2, p0 = n0 + 1, n1 = n0 + 2, p1 = n0 + 3;
                    if (n1 >= vertexCount)
                    {
                        n1 = 0; p1 = 1;
                    }

                    m_indices[iidx + i * 6 + 0] = (ushort)(vidx + n0);
                    m_indices[iidx + i * 6 + 1] = (ushort)(vidx + p0);
                    m_indices[iidx + i * 6 + 2] = (ushort)(vidx + p1);
                    m_indices[iidx + i * 6 + 3] = (ushort)(vidx + n0);
                    m_indices[iidx + i * 6 + 4] = (ushort)(vidx + p1);
                    m_indices[iidx + i * 6 + 5] = (ushort)(vidx + n1);
                }

                m_vertexCount += vertexCount;
                m_indexCount += indexCount;
            }
        }

        private void AddTriangle(VertexRB2D v0, VertexRB2D v1, VertexRB2D v2)
        {
            if (m_indexCount + 3 >= MaxVertexCount || m_vertexCount + 3 >= MaxVertexCount)
                Flush();

            int vidx = m_vertexCount;
            m_vertices[vidx + 0] = v0;
            m_vertices[vidx + 1] = v1;
            m_vertices[vidx + 2] = v2;

            int iidx = m_indexCount;
            m_indices[iidx + 0] = (ushort)(vidx + 0);
            m_indices[iidx + 1] = (ushort)(vidx + 1);
            m_indices[iidx + 2] = (ushort)(vidx + 2);

            m_vertexCount += 3;
            m_indexCount += 3;
        }

        private void AddQuad(VertexRB2D v0, VertexRB2D v1, VertexRB2D v2, VertexRB2D v3)
        {
            if (m_indexCount + 6 >= MaxVertexCount || m_vertexCount + 4 >= MaxVertexCount)
                Flush();

            int vidx = m_vertexCount;
            m_vertices[vidx + 0] = v0;
            m_vertices[vidx + 1] = v1;
            m_vertices[vidx + 2] = v2;
            m_vertices[vidx + 3] = v3;

            int iidx = m_indexCount;
            m_indices[iidx + 0] = (ushort)(vidx + 0);
            m_indices[iidx + 1] = (ushort)(vidx + 1);
            m_indices[iidx + 2] = (ushort)(vidx + 2);
            m_indices[iidx + 3] = (ushort)(vidx + 0);
            m_indices[iidx + 4] = (ushort)(vidx + 2);
            m_indices[iidx + 5] = (ushort)(vidx + 3);

            m_vertexCount += 4;
            m_indexCount += 6;
        }

        public void FillRectangle(float x, float y, float w, float h)
        {
            var cmds = new Path2DCommands();
            cmds.MoveTo(x, y);
            cmds.LineTo(x + w, y);
            cmds.LineTo(x + w, y + h);
            cmds.LineTo(x, y + h);
            cmds.Close();

            var paths = Flatten(cmds);
            Fill(paths);
        }

        public void StrokeRectangle(float x, float y, float w, float h, float strokePos, float strokeNeg)
        {
            var cmds = new Path2DCommands();
            cmds.MoveTo(x, y);
            cmds.LineTo(x + w, y);
            cmds.LineTo(x + w, y + h);
            cmds.LineTo(x, y + h);
            cmds.Close();

            var paths = Flatten(cmds);
            Stroke(paths, strokePos, strokeNeg);
        }

        public void FillRoundedRectangle(float x, float y, float w, float h, float r)
        {
            FillRoundedRectangleVarying(x, y, w, h, r, r, r, r);
        }

        public void FillRoundedRectangleVarying(float x, float y, float w, float h, float rtl, float rtr, float rbr, float rbl)
        {
            const float k = Kappa90;
            var cmds = new Path2DCommands();
            cmds.MoveTo(x + rtl, y);

            cmds.LineTo(x + w - rtr, y);
            cmds.BezierTo(x + w - rtr * (1 - k), y, x + w, y + rtr * (1 - k), x + w, y + rtr);

            cmds.LineTo(x + w, y + h - rbr);
            cmds.BezierTo(x + w, y + h - rbr * (1 - k), x + w - rbr * (1 - k), y + h, x + w - rbr, y + h);

            cmds.LineTo(x + rbl, y + h);
            cmds.BezierTo(x + rbl * (1 - k), y + h, x, y + h - rbl * (1 - k), x, y + h - rbl);

            cmds.LineTo(x, y + rtl);
            cmds.BezierTo(x, y + rtl * (1 - k), x + rtl * (1 - k), y, x + rtl, y);

            cmds.Close();

            var paths = Flatten(cmds);
            Fill(paths);
        }

        public void StrokeRoundedRectangleVarying(float x, float y, float w, float h, float strokePos, float strokeNeg, float rtl, float rtr, float rbr, float rbl)
        {
            const float k = Kappa90;
            var cmds = new Path2DCommands();
            cmds.MoveTo(x + rtl, y);

            cmds.LineTo(x + w - rtr, y);
            cmds.BezierTo(x + w - rtr * (1 - k), y, x + w, y + rtr * (1 - k), x + w, y + rtr);

            cmds.LineTo(x + w, y + h - rbr);
            cmds.BezierTo(x + w, y + h - rbr * (1 - k), x + w - rbr * (1 - k), y + h, x + w - rbr, y + h);

            cmds.LineTo(x + rbl, y + h);
            cmds.BezierTo(x + rbl * (1 - k), y + h, x, y + h - rbl * (1 - k), x, y + h - rbl);

            cmds.LineTo(x, y + rtl);
            cmds.BezierTo(x, y + rtl * (1 - k), x + rtl * (1 - k), y, x + rtl, y);

            cmds.Close();

            var paths = Flatten(cmds);
            Stroke(paths, strokePos, strokeNeg);
        }
    }
}
