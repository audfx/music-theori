using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

using theori.Graphics.OpenGL;
using theori.Resources;

using static MoonSharp.Interpreter.DynValue;

namespace theori.Graphics
{
    [MoonSharpUserData]
    public sealed class BasicSpriteRenderer : Disposable
    {
        class TextRasterizerBatch : Disposable
        {
            private readonly List<TextRasterizer> m_inUse = new List<TextRasterizer>();
            private readonly List<TextRasterizer> m_available = new List<TextRasterizer>();

            public TextRasterizer Aquire((Font Font, float Size, string Text)? match = null)
            {
                TextRasterizer? result = null;
                if (match != null)
                {
                    for (int i = 0; i < m_available.Count; i++)
                    {
                        var tr = m_available[i];
                        if (tr.Font == match?.Font! && tr.Size == match?.Size! && tr.Text == match?.Text!)
                            result = tr;
                    }
                }

                if (result == null)
                {
                    if (m_available.Count > 0)
                    {
                        result = m_available[m_available.Count - 1];
                        m_available.RemoveAt(m_available.Count - 1);
                    }
                    else result = new TextRasterizer();
                }

                m_inUse.Add(result!);
                return result;
            }

            public void Release(TextRasterizer rasterizer)
            {
                Debug.Assert(m_inUse.Contains(rasterizer));
                m_inUse.Remove(rasterizer);
                m_available.Add(rasterizer);
            }

            public void ReleaseAll()
            {
                m_available.AddRange(m_inUse);
                m_inUse.Clear();
            }

            protected override void DisposeManaged()
            {
                foreach (var tr in m_inUse)
                    tr.Dispose();
                foreach (var tr in m_available)
                    tr.Dispose();

                m_inUse.Clear();
                m_available.Clear();
            }
        }

        private Vector2? m_viewport;

        private Mesh m_rectMesh = Host.StaticResources.Manage(Mesh.CreatePlane(Vector3.UnitX, Vector3.UnitY, 1, 1, Anchor.TopLeft));

        private ClientResourceManager m_resources;
        private Material m_basicMaterial;

        private Vector4 m_drawColor = Vector4.One, m_imageColor = Vector4.One;
        private Transform m_transform = Transform.Identity;
        private Anchor m_textAlign = Anchor.TopLeft;

        private readonly Stack<Transform> m_savedTransforms = new Stack<Transform>();

        private RenderQueue? m_queue;
        private Font m_font = Font.Default;
        private float m_fontSize = 16.0f;

        private readonly TextRasterizerBatch m_trBatch = new TextRasterizerBatch();
        private readonly Dictionary<Font, TextRasterizer> m_labels = new Dictionary<Font, TextRasterizer>();

        [MoonSharpHidden]
        public BasicSpriteRenderer(ClientResourceLocator? locator = null, Vector2? viewportSize = null)
        {
            m_viewport = viewportSize;
            m_resources = new ClientResourceManager(locator ?? ClientResourceLocator.Default);
            m_basicMaterial = m_resources.AquireMaterial("materials/basic");
        }

        protected override void DisposeManaged()
        {
            Flush();

            m_queue?.Dispose();
            m_queue = null;

            m_trBatch.Dispose();

            m_rectMesh.Dispose();
            m_resources.Dispose();
        }

        #region Lua Bound Functions

        public void Flush() => m_queue?.Process(true);

        public void BeginFrame()
        {
            m_transform = Transform.Identity;
            m_drawColor = Vector4.One;
            m_imageColor = Vector4.One;
            m_textAlign = Anchor.TopLeft;

            SetFont(null);
            SetFontSize(16);

            Vector2 viewportSize = m_viewport ?? new Vector2(Window.Width, Window.Height);
            m_queue = new RenderQueue(new RenderState
            {
                ProjectionMatrix = (Transform)Matrix4x4.CreateOrthographicOffCenter(0, viewportSize.X, viewportSize.Y, 0, -10, 10),
                CameraMatrix = Transform.Identity,
                ViewportSize = ((int)viewportSize.X, (int)viewportSize.Y),
            });
        }

        public void EndFrame()
        {
            Flush();

            m_queue!.Dispose();
            m_queue = null;

            m_savedTransforms.Clear();

            m_trBatch.ReleaseAll();
        }

        public void SaveTransform()
        {
            m_savedTransforms.Push(m_transform);
        }

        public void RestoreTransform()
        {
            if (m_savedTransforms.Count == 0) return;
            m_transform = m_savedTransforms.Pop();
        }

        public void ResetTransform()
        {
            m_savedTransforms.Clear();
            m_transform = Transform.Identity;
        }

        public void Translate(float x, float y)
        {
            m_transform = m_transform * Transform.Translation(x, y, 0);
        }

        public void Rotate(float rDeg)
        {
            m_transform = m_transform * Transform.RotationZ(rDeg);
        }

        public void Scale(float s) => Scale(s, s);
        public void Scale(float sx, float sy)
        {
            m_transform = m_transform * Transform.Scale(sx, sy, 1);
        }

        public void Shear(float sx, float sy)
        {
            var shear = Matrix4x4.Identity;
            shear.M21 = sx;
            shear.M12 = sy;

            m_transform = m_transform * new Transform(shear);
        }

        [MoonSharpVisible(true)]
        private DynValue GetViewportSize()
        {
            if (m_viewport is Vector2 v)
                return NewTuple(NewNumber(v.X), NewNumber(v.Y));
            else return NewTuple(NewNumber(Window.Width), NewNumber(Window.Height));
        }

        public void SetColor(float r, float g, float b) => SetColor(r, g, b, 255);

        public void SetColor(float r, float g, float b, float a)
        {
            m_drawColor = new Vector4(r, g, b, a) / 255.0f;
        }

        public void FillRect(float x, float y, float w, float h)
        {
            var transform = Transform.Scale(w, h, 1) * Transform.Translation(x, y, 0);

            var p = new MaterialParams();
            p["MainTexture"] = Texture.Empty;
            p["Color"] = m_drawColor;

            m_queue!.Draw(transform * m_transform, m_rectMesh, m_basicMaterial, p);
        }

        public void SetImageColor(float r, float g, float b) => SetImageColor(r, g, b, 255);

        public void SetImageColor(float r, float g, float b, float a)
        {
            m_imageColor = new Vector4(r, g, b, a) / 255.0f;
        }

        public void Image(Texture texture, float x, float y, float w, float h)
        {
            var transform = Transform.Scale(w, h, 1) * Transform.Translation(x, y, 0);

            var p = new MaterialParams();
            p["MainTexture"] = texture;
            p["Color"] = m_imageColor;

            m_queue!.Draw(transform * m_transform, m_rectMesh, m_basicMaterial, p);
        }

        public void SetFont(Font? font)
        {
            if (font == null) font = Font.Default;
            m_font = font;
        }

        public void SetFontSize(float size)
        {
            m_fontSize = size;
        }

        public void SetTextAlign(Anchor align)
        {
            m_textAlign = align;
        }

        public void Write(string text, float x, float y)
        {
            var rasterizer = m_trBatch.Aquire((m_font, m_fontSize, text));
            rasterizer.Font = m_font;
            rasterizer.Size = m_fontSize;
            rasterizer.Text = text;

            rasterizer.Rasterize();

            Vector2 offset = Vector2.Zero, size = new Vector2(rasterizer.Width, rasterizer.Height);
            switch ((Anchor)((int)m_textAlign & 0x0F))
            {
                case Anchor.Top: break;
                case Anchor.Middle: offset.Y = (int)(-size.Y / 2); break;
                case Anchor.Bottom: offset.Y = -size.Y; break;
            }

            switch ((Anchor)((int)m_textAlign & 0xF0))
            {
                case Anchor.Left: break;
                case Anchor.Center: offset.X = (int)(-size.X / 2); break;
                case Anchor.Right: offset.X = -size.X; break;
            }

            var transform = Transform.Scale(rasterizer.Width, rasterizer.Height, 1)
                          * Transform.Translation(x + offset.X, y + offset.Y, 0);

            var p = new MaterialParams();
            p["MainTexture"] = rasterizer.Texture;
            p["Color"] = m_drawColor;

            m_queue!.Draw(transform * m_transform, m_rectMesh, m_basicMaterial, p);
        }

        #endregion
    }
}
