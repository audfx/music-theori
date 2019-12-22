using System;
using System.Collections.Generic;
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
        private Vector2? m_viewport;

        private Mesh m_rectMesh = Host.StaticResources.Manage(Mesh.CreatePlane(Vector3.UnitX, Vector3.UnitY, 1, 1, Anchor.TopLeft));

        private ClientResourceManager m_resources;
        private Material m_basicMaterial;

        private Vector4 m_drawColor = Vector4.One, m_imageColor = Vector4.One;
        private Transform m_transform = Transform.Identity;
        private Rect? m_scissor = null;
        private Anchor m_textAlign = Anchor.TopLeft;

        private readonly Stack<int> m_savedTransformIndices = new Stack<int>();
        private readonly List<Transform?> m_transformations = new List<Transform?>();
        private readonly Stack<Rect?> m_savedScissors = new Stack<Rect?>();

        private RenderQueue? m_queue;

        private FontCollection m_font = FontCollection.Default;
        private int m_fontSize = 48;

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

            m_rectMesh.Dispose();
            m_resources.Dispose();
        }

        #region Lua Bound Functions

        public void Flush() => m_queue?.Process(true);

        public void BeginFrame()
        {
            m_transform = Transform.Identity;
            m_scissor = null;
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
                Viewport = (0, 0, (int)viewportSize.X, (int)viewportSize.Y),
            });
        }

        public void EndFrame()
        {
            Flush();

            m_queue!.Dispose();
            m_queue = null;

            m_transformations.Clear();
            m_savedTransformIndices.Clear();
            m_savedScissors.Clear();
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
            m_scissor = m_savedScissors.Pop();
        }

        public void ResetScissor()
        {
            m_savedScissors.Clear();
            m_scissor = null;
        }

        public void Scissor(float x, float y, float w, float h)
        {
            if (m_scissor is null)
                m_scissor = new Rect(x, y, w, h);
            else
            {
                //m_scissor = new Rect();
            }
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
            p["TempMappedTextureCoords"] = new Vector4(0, 0, 1, 1);
            p["Color"] = m_drawColor;

            if (m_scissor is Rect scissor)
                m_queue!.Draw(scissor, transform * m_transform, m_rectMesh, m_basicMaterial, p);
            else m_queue!.Draw(transform * m_transform, m_rectMesh, m_basicMaterial, p);
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
            p["TempMappedTextureCoords"] = new Vector4(0, 0, 1, 1);
            p["Color"] = m_imageColor;

            if (m_scissor is Rect scissor)
                m_queue!.Draw(scissor, transform * m_transform, m_rectMesh, m_basicMaterial, p);
            else m_queue!.Draw(transform * m_transform, m_rectMesh, m_basicMaterial, p);
        }

        public void SetFont(FontCollection? font)
        {
            if (font == null) font = FontCollection.Default;
            m_font = font;
        }

        public void SetFontSize(int size)
        {
            m_fontSize = size;
        }

        public void SetTextAlign(Anchor align)
        {
            m_textAlign = align;
        }

        public void Write(string text, float x, float y)
        {
            // sort by texture to facilitate batching in the future
            Dictionary<Texture, List<(int X, int Y, (int X, int Y, GlyphInfo Info) GlyphInfo)>> glyphs = new Dictionary<Texture, List<(int, int, (int, int, GlyphInfo))>>();

            var atlas = m_font.GetFontAtlas(m_fontSize);
            int dx = 0, dh = 0;

            foreach (char c in text)
            {
                if (c == ' ')
                {
                    dx += atlas.SpaceWidth;
                    continue;
                }

                var info = atlas.GetTextureInfoForCharacter(c);
                if (!glyphs.TryGetValue(info.Texture, out var glyphList))
                    glyphList = glyphs[info.Texture] = new List<(int, int, (int, int, GlyphInfo))>();

                glyphList.Add((dx, 0, (info.X, info.Y, info.Info)));
                dx += info.Info.Width;
                dh = MathL.Max(dh, info.Info.Height);
            }

            Vector2 offset = Vector2.Zero, size = new Vector2(dx, dh);
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

            foreach (var (texture, glyphList) in glyphs)
            {
                foreach (var glyph in glyphList)
                {
                    var p = new MaterialParams();
                    p["MainTexture"] = texture;
                    p["TempMappedTextureCoords"] = new Vector4(glyph.GlyphInfo.X / (float)texture.Width, (glyph.GlyphInfo.Y) / (float)texture.Height,
                        glyph.GlyphInfo.Info.Width / (float)texture.Width, glyph.GlyphInfo.Info.Height / (float)texture.Height);
                    p["Color"] = m_drawColor;

                    var transform = Transform.Scale(glyph.GlyphInfo.Info.Width, glyph.GlyphInfo.Info.Height, 1)
                            * Transform.Translation(x + glyph.X + offset.X, y + glyph.Y + offset.Y, 0);

                    if (m_scissor is Rect scissor)
                        m_queue!.Draw(scissor, transform * m_transform, m_rectMesh, m_basicMaterial, p);
                    else m_queue!.Draw(transform * m_transform, m_rectMesh, m_basicMaterial, p);
                }
            }

#if false
            var rasterizer = new TextRasterizer(m_font, m_fontSize, text);
            rasterizer.Rasterize();

            m_rasterizers.Add(rasterizer);

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

            if (m_scissor is Rect scissor)
                m_queue!.Draw(scissor, transform * m_transform, m_rectMesh, m_basicMaterial, p);
            else m_queue!.Draw(transform * m_transform, m_rectMesh, m_basicMaterial, p);
#endif
        }

#endregion
    }
}
