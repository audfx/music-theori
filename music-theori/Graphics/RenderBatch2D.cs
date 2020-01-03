using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using LibTessDotNet;
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

        private VectorFont m_font = VectorFont.Default;
        private float m_fontSize = 64.0f;

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

        public void SetFont(VectorFont? font, float? size = null)
        {
            if (size is float sizeValue)
                m_fontSize = sizeValue;
            m_font = font ?? VectorFont.Default;
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

        private void Fill(Path2DGroup pathGroup, Vector2 offset = default)
        {
            var paths = pathGroup.Paths;

            var t = new Tess();
            foreach (var path in paths)
            {
                var cVerts = path.Points.Select(pt => new ContourVertex(new Vec3(pt.Position.X + offset.X, pt.Position.Y + offset.Y, 0)));
                t.AddContour(cVerts.ToArray(), path.Winding == AngularDirection.Clockwise ? ContourOrientation.Clockwise : ContourOrientation.CounterClockwise);
            }

            t.Tessellate();

            int vidx = m_vertexCount;
            for (int i = 0; i < t.Vertices.Length; i++)
            {
                var v = t.Vertices[i];
                m_vertices[vidx + i] = new VertexRB2D(32, new Vector2(v.Position.X, v.Position.Y), Vector2.Zero, Vector4.One);
            }

            int iidx = m_indexCount;
            for (int i = 0; i < t.Elements.Length; i++)
                m_indices[iidx + i] = (ushort)(vidx + t.Elements[i]);

            m_vertexCount += t.Vertices.Length;
            m_indexCount += t.Elements.Length;
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

            var paths = cmds.Flatten();
            Fill(paths);
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
            cmds.CubicBezierTo(x + w - rtr * (1 - k), y, x + w, y + rtr * (1 - k), x + w, y + rtr);

            cmds.LineTo(x + w, y + h - rbr);
            cmds.CubicBezierTo(x + w, y + h - rbr * (1 - k), x + w - rbr * (1 - k), y + h, x + w - rbr, y + h);

            cmds.LineTo(x + rbl, y + h);
            cmds.CubicBezierTo(x + rbl * (1 - k), y + h, x, y + h - rbl * (1 - k), x, y + h - rbl);

            cmds.LineTo(x, y + rtl);
            cmds.CubicBezierTo(x, y + rtl * (1 - k), x + rtl * (1 - k), y, x + rtl, y);

            cmds.Close();

            var paths = cmds.Flatten();
            Fill(paths);
        }

        public void DrawString(string text, float x, float y)
        {
            float xOff = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!m_font.TryGetGlyphData(text[i], out var info, out var cmds))
                    continue;

                float pixelsPerEm = m_fontSize;
                float scale = m_fontSize / info.EmSize;

                float advance = pixelsPerEm * info.AdvanceWidth / info.EmSize;

                if (text[i] != ' ')
                {
                    var paths = cmds.Flatten(scale);
                    Fill(paths, new Vector2(x + xOff, y));
                }

                xOff += advance;
            }
        }
    }
}
