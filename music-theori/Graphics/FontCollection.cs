using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using theori.Graphics.OpenGL;

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using SL_Font = SixLabors.Fonts.Font;
using theori.Resources;

namespace theori.Graphics
{
    public struct GlyphInfo
    {
        public int Width, Height;
        public int AdvanceWidth;

        public int BaseLine;
    }

    public class FontCollection
    {
        public static readonly FontCollection Default;

        static FontCollection()
        {
            //Default = new Font("Arial");

            Default = new FontCollection(ClientResourceLocator.Default.OpenFileStream("fonts/osaka.unicode.ttf"), 8);
            Default.GetFontAtlas(16);
            Default.GetFontAtlas(24);
            Default.GetFontAtlas(48);
            Default.GetFontAtlas(64);
        }

        public class Atlas
        {
            private readonly SL_Font m_font;

            private readonly int m_recResolution;
            private readonly List<Texture> m_pages = new List<Texture>();

            internal IEnumerable<Texture> DebugAllTextures => m_pages;

            public int SpaceWidth => (int)m_font.GetGlyph(' ').BoundingBox(Vector2.Zero, new Vector2(96)).Width;

            private readonly Dictionary<char, (int X, int Y, int TexId, GlyphInfo Info)> m_chars = new Dictionary<char, (int, int, int, GlyphInfo)>();

            private Texture CurrentTexture => m_pages[^1];

            private int m_curX, m_curY, m_curLineHeight;
            private Image<Rgba32> m_curImage;

            private bool m_changes = false;

            public Atlas(SL_Font font, int recResolution = 1024)
            {
                m_font = font;
                m_recResolution = recResolution;
                m_curImage = new Image<Rgba32>(m_recResolution, m_recResolution);

                AddPage();
                AddCharacters("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789,<.>/?;:'\"\\|]}[{`~_-=+!@#$%^&^*()");
            }

            private void AddPage()
            {
                var page = new Texture();
                m_pages.Add(page);

                m_curX = m_curY = m_curLineHeight = 0;
                for (int x = 0; x < m_curImage.Width; x++) for (int y = 0; y < m_curImage.Height; y++)
                    m_curImage[x, y] = new Rgba32(255, 255, 255, 0);

                Commit();
            }

            public void Commit()
            {
                if (!m_changes) return;
                m_changes = false;

                for (int x = 0; x < m_curImage.Width; x++)  for (int y = 0; y < m_curImage.Height; y++)
                    m_curImage[x, y] = new Rgba32(255, 255, 255, m_curImage[x, y].A);

                CurrentTexture.Create2DFromImage(m_curImage);
            }

            public void AddCharacters(string characters) => AddCharacters(characters.AsEnumerable());
            public void AddCharacters(params char[] characters) => AddCharacters((IEnumerable<char>)characters);

            public void AddCharacters(IEnumerable<char> characters)
            {
                foreach (char c in characters)
                    AddCharacter(c);

                Commit();
            }

            private (int X, int Y, int TexId, GlyphInfo Info) AddCharacter(char c)
            {
                if (!m_chars.TryGetValue(c, out var info))
                {
                    m_changes = true;

                    var glyphInfo = m_font.GetGlyph(c);

                    int cx = m_curX, cy = m_curY;
                    var box = glyphInfo.BoundingBox(Vector2.Zero, new Vector2(96));
                    int baseLine = MathL.CeilToInt(-box.Y);
                    int cw = MathL.CeilToInt(box.Width), cw2 = MathL.CeilToInt(box.Width + glyphInfo.Instance.LeftSideBearing / m_font.Size), ch = MathL.CeilToInt(box.Height);

                    if (cx + cw >= m_curImage.Width)
                    {
                        m_curX = cx = 0;
                        m_curY = cy = m_curY + m_curLineHeight;
                    }

                    if (cy + ch >= m_curImage.Height)
                    {
                        AddPage();
                        cx = cy = 0;
                    }

                    var opts = new TextGraphicsOptions
                    {
                        Antialias = true,
                        AntialiasSubpixelDepth = 8,
                        ApplyKerning = false,
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        //DpiX = 96, DpiY = 96,
                    };
                    m_curImage.Mutate(x => x.DrawText(opts, c.ToString(), m_font, new Rgba32(255, 255, 255, 255), new Vector2(cx, cy)));

                    m_curX += cw2 + 1;
                    m_curLineHeight = MathL.Max(m_curLineHeight, ch + 1);

                    m_chars[c] = (cx, cy, m_pages.Count - 1, new GlyphInfo()
                    {
                        Width = cw, Height = ch,
                        AdvanceWidth = MathL.CeilToInt(glyphInfo.Instance.AdvanceWidth / m_font.Size),
                        BaseLine = baseLine,
                    });
                }

                return info;
            }

            public (int X, int Y, Texture Texture, GlyphInfo Info) GetTextureInfoForCharacter(char c)
            {
                if (!m_chars.TryGetValue(c, out var info))
                    info = AddCharacter(c);

                Commit();
                return (info.X, info.Y, m_pages[info.TexId], info.Info);
            }
        }

        private readonly SixLabors.Fonts.FontCollection m_collection = new SixLabors.Fonts.FontCollection();
        private readonly FontFamily m_family;

        private readonly Dictionary<(int Size, FontStyle Style), Atlas> m_fonts = new Dictionary<(int, FontStyle), Atlas>();

        public FontCollection(string systemFont, int defaultSize = 16)
        {
            m_family = SystemFonts.Families.Where(family => family.Name == systemFont).Select(family => (FontFamily?)family).SingleOrDefault()
                ?? throw new ArgumentException(nameof(systemFont));
            GetFontAtlas(defaultSize);
        }

        public FontCollection(Stream fontStream, int defaultSize = 16)
        {
            m_family = m_collection.Install(fontStream);
            GetFontAtlas(defaultSize);
        }

        public Atlas GetFontAtlas(int size, FontStyle style = FontStyle.Regular)
        {
            var key = (size, style);
            if (m_fonts.ContainsKey(key))
                return m_fonts[key];

            var font = m_family.CreateFont(size);
            return m_fonts[key] = new Atlas(font, size * 16);
        }
    }
}
