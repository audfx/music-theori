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
        public const int MaxPaints = 16, MaxTextures = 16;
        public const int PaintFillKind = 0, TextureFillKind = 16, SolidFillKind = 17;

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
        private Texture? m_texture = null;

        private VectorFont m_font = VectorFont.Default;
        private float m_fontSize = 64.0f;

        private int m_fillKind = 32;
        private Vector4 m_vertexColor = Vector4.One;

        private Transform m_transform = Transform.Identity;
        private Rect? m_scissor = null;
        private Anchor m_textAlign = Anchor.TopLeft;

        private readonly Stack<int> m_savedTransformIndices = new Stack<int>();
        private readonly List<Transform?> m_transformations = new List<Transform?>();
        private readonly Stack<Rect?> m_savedScissors = new Stack<Rect?>();

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

        public void SetFontSize(float size)
        {
            m_fontSize = size;
        }

        public void SetTextAlign(Anchor align)
        {
            m_textAlign = align;
        }

        internal void Begin()
        {
            m_vertexCount = 0;
            m_indexCount = 0;

            m_world = Transform.Identity;
            m_projection = (Transform)Matrix4x4.CreateOrthographicOffCenter(0, Window.Width, Window.Height, 0, -10, 10);
            m_camera = Transform.Identity;

            m_texture = null;

            m_fillKind = SolidFillKind;
            m_vertexColor = Vector4.One;

            m_transform = Transform.Identity;
            m_scissor = null;
            m_transformations.Clear();
            m_savedTransformIndices.Clear();
            m_savedScissors.Clear();

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

        public void SaveTransform()
        {
            Flush();

            m_savedTransformIndices.Push(m_transformations.Count);
        }

        public void RestoreTransform()
        {
            if (m_savedTransformIndices.Count == 0) return;

            Flush();

            int transformsCount = m_savedTransformIndices.Pop();
            m_transformations.RemoveRange(transformsCount, m_transformations.Count - transformsCount);

            UpdateTransform();
        }

        public void ResetTransform()
        {
            Flush();

            m_transformations.Add(null);

            UpdateTransform();
        }

        public void Translate(float x, float y)
        {
            m_transformations.Add(Transform.Translation(x, y, 0));
            UpdateTransform();
        }

        public void Rotate(float rDeg)
        {
            m_transformations.Add(Transform.RotationZ(rDeg));
            UpdateTransform();
        }

        public void Scale(float s) => Scale(s, s);
        public void Scale(float sx, float sy)
        {
            m_transformations.Add(Transform.Scale(sx, sy, 1));
            UpdateTransform();
        }

        public void Shear(float sx, float sy)
        {
            var shear = Matrix4x4.Identity;
            shear.M21 = sx;
            shear.M12 = sy;

            m_transformations.Add(new Transform(shear));
            UpdateTransform();
        }

        private void UpdateTransform()
        {
            m_transform = Transform.Identity;
            for (int i = m_transformations.Count - 1; i >= 0; i--)
            {
                if (m_transformations[i] == null)
                    break;
                m_transform = m_transformations[i]!.Value * m_transform;
            }
        }

        public void SaveScissor()
        {
            m_savedScissors.Push(m_scissor);
        }

        public void RestoreScissor()
        {
            if (m_savedScissors.Count == 0) return;
            
            Flush();

            m_scissor = m_savedScissors.Pop();
        }

        public void ResetScissor()
        {
            Flush();

            m_savedScissors.Clear();
            m_scissor = null;
        }

        public void Scissor(float x, float y, float w, float h)
        {
            Flush();

            if (m_scissor is null)
                m_scissor = new Rect(x, y, w, h);
            else
            {
                //m_scissor = new Rect();
            }
        }

        public void Flush()
        {
            using var _ = Profiler.Scope(nameof(Flush));

            var indices = new ushort[m_indexCount];
            var vertices = new VertexRB2D[m_vertexCount];

            Array.Copy(m_indices, 0, indices, 0, m_indexCount);
            Array.Copy(m_vertices, 0, vertices, 0, m_vertexCount);

            m_mesh.SetIndices(indices);
            m_mesh.SetVertices(vertices);

            Profiler.Instant("Finished allocating memory for vertex data");

            m_params["Texture"] = 0;
            if (m_texture is Texture tex)
                tex.Bind(0);
            else Texture.Unbind(TextureTarget.Texture2D, 0);

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
            Profiler.Instant("Bound material");

            {
                using var _2 = Profiler.Scope("Drawing batch");

                GL.Enable(GL.GL_BLEND);
                GL.Enable(GL.GL_DEPTH_TEST);
                GL.DepthFunc(DepthFunction.LessThanOrEqual);
                GL.BlendFuncSeparate(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA, GL.GL_ONE, GL.GL_ONE);

                if (m_scissor is Rect s && s.Width >= 0)
                {
                    GL.Enable(GL.GL_SCISSOR_TEST);

                    float scissorY = Window.Height - s.Bottom;
                    GL.Scissor((int)s.Left, (int)scissorY,
                               (int)s.Width, (int)s.Height);
                }
                else GL.Disable(GL.GL_SCISSOR_TEST);

                m_mesh.Draw();
                GL.Disable(GL.GL_BLEND);
                GL.Disable(GL.GL_DEPTH_TEST);
            }

            m_indexCount = 0;
            m_vertexCount = 0;

            m_texture = null;
        }

        public void SetFillColor(float r, float g, float b, float a) => SetFillColor(new Vector4(r, g, b, a));
        public void SetFillColor(Vector4 tint)
        {
            m_fillKind = SolidFillKind;
            m_vertexColor = tint;
        }

        public void SetFillTexture(Texture texture, Vector4 tint)
        {
            if (m_texture != null && texture != m_texture)
                Flush();

            m_texture = texture;

            m_fillKind = TextureFillKind;
            m_vertexColor = tint;
        }

        public void SetPaintTexture(Texture texture)
        {
            // NOTE(local): this signature is only here because I want to remind myself of the difference between the term "fill" and "paint" in this renderer
            throw new NotImplementedException();
        }

        private void Fill(Path2DGroup pathGroup, Vector2 offset = default, Vector2? scale = null)
        {
            var s = scale ?? Vector2.One;
            using var _ = Profiler.Scope(nameof(Fill));

            var __getPathVerts = Profiler.Scope("Get path group vertices");
            var pathVerts = pathGroup.GetVertices().Select(v => new VertexRB2D(m_fillKind, Vector2.Transform(v.Position * s + offset, m_transform.Matrix), v.TexCoord, m_vertexColor)).ToArray();
            __getPathVerts?.Dispose();

            if (m_vertexCount + pathVerts.Length >= m_vertices.Length
            ||  m_indexCount + pathGroup.GetIndices().Count >= m_indices.Length)
            {
                Flush();
            }

            var __copyVerticesToArray = Profiler.Scope("Copy processed vertices to output buffer");
            int vidx = m_vertexCount;
            foreach (var vert in pathVerts)
                m_vertices[vidx++] = vert;
            __copyVerticesToArray?.Dispose();

            var __copyIndicesToArray = Profiler.Scope("Copy indices to output buffer");
            int iidx = m_indexCount;
            foreach (ushort index in pathGroup.GetIndices())
                m_indices[iidx++] = (ushort)(m_vertexCount + index);
            __copyIndicesToArray?.Dispose();

            m_vertexCount = vidx;
            m_indexCount = iidx;
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

        public void FillPath(Path2DCommands cmds)
        {
            var paths = cmds.Flatten();
            paths.SetTextureCoordsToGroupLocal();
            Fill(paths);
        }

        public void FillPathAt(Path2DCommands cmds, float x, float y, float sx, float sy)
        {
            var paths = cmds.Flatten(sx, sy);
            paths.SetTextureCoordsToGroupLocal();
            Fill(paths, new Vector2(x, y));
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
            paths.SetTextureCoordsToGroupLocal();
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
            paths.SetTextureCoordsToGroupLocal();
            Fill(paths);
        }

        public void FillString(string text, float x, float y)
        {
            using var _ = Profiler.Scope(nameof(FillString));

            float scale = m_fontSize / m_font.EmSize;
            var bounds = MeasureString(text);

            Profiler.Instant("Calculate offsets from alignment");
            float xOffset = 0, yOffset = 0;
            switch ((Anchor)((int)m_textAlign & 0x0F))
            {
                case Anchor.Top: yOffset = bounds.Y; break;
                case Anchor.Middle: yOffset = (int)(bounds.Y / 2); break;
                case Anchor.Bottom: break;
            }

            switch ((Anchor)((int)m_textAlign & 0xF0))
            {
                case Anchor.Left: break;
                case Anchor.Center: xOffset = (int)(-bounds.X / 2); break;
                case Anchor.Right: xOffset = -bounds.X; break;
            }

            Profiler.Instant("Begin posting glyphs for render");
            float xPosition = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!m_font.TryGetGlyphData(text[i], out var info, out var cmds))
                    continue;

                if (text[i] != ' ')
                {
                    var paths = cmds.Flatten();
                    if (paths.Paths.Length > 0)
                    {
                        var offs = new Vector2(x + xPosition + xOffset, y + yOffset);
                        Fill(paths, offs, new Vector2(scale));
                    }
                }

                xPosition += scale * info.AdvanceWidth;
            }
        }

        public Vector2 MeasureString(string text)
        {
            using var _ = Profiler.Scope(nameof(MeasureString));

            float scale = m_fontSize / m_font.EmSize;

            Profiler.Instant("Get initial glyph data for bounds");
            float xBounds = 0, yBounds = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!m_font.TryGetGlyphData(text[i], out var info, out var cmds))
                    continue;

                xBounds += scale * info.AdvanceWidth;
                yBounds = MathL.Max(yBounds, info.LineHeight * scale);
            }

            return new Vector2(xBounds, yBounds);
        }
    }
}
