using System;
using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using theori.Graphics.OpenGL;

namespace theori.Graphics
{
    public class TextRasterizer : IDisposable
    {
        public bool IsDirty { get; private set; } = true;

        private string m_text;
        private Font m_font;
        private float m_size;

        private readonly Texture m_texture = new Texture();
        public Texture Texture
        {
            get
            {
                if (IsDirty)
                    Rasterize();
                return m_texture;
            }
        }

        public string Text
        {
            get => m_text;
            set
            {
                if (value == m_text)
                    return;

                for (int i = 0; i < value.Length; i++)
                {
                    if (char.IsSurrogate(value[i]))
                    {
                        var builder = new StringBuilder();

                        builder.Append(value, 0, i);
                        builder.Append('?');

                        i++;

                        for (int j = i + 1; j < value.Length; j++)
                        {
                            if (char.IsSurrogate(value[j]))
                            {
                                builder.Append('?');
                                j++;
                            }
                            else builder.Append(value[j]);
                        }

                        value = builder.ToString();
                        break;
                    }
                }

                m_text = value;
                IsDirty = true;
            }
        }

        public Font Font
        {
            get => m_font;
            set
            {
                if (value == m_font)
                    return;

                m_font = value;
                IsDirty = true;
            }
        }

        public float Size
        {
            get => m_size;
            set
            {
                if (value == m_size)
                    return;

                m_size = value;
                IsDirty = true;
            }
        }

        public float BaseLine { get; private set; }

        public int Width => m_texture.Width;
        public int Height => m_texture.Height;

        public TextRasterizer()
            : this(Font.Default)
        {
        }

        public TextRasterizer(Font font, float size = 16.0f, string text = "")
        {
            Text = text;

            m_font = font;
            m_size = size;
        }

        public void Dispose()
        {
            m_texture.Dispose();
        }

        public void Rasterize()
        {
            var opts = new TextGraphicsOptions()
            {
                Antialias = true,
                AntialiasSubpixelDepth = 8,
                ApplyKerning = true,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            var font = m_font.GetNearestLoadedFont(m_size);
            var bounds = TextMeasurer.MeasureBounds(m_text, new RendererOptions(font));

            using var img = new Image<Rgba32>(MathL.CeilToInt(bounds.Width), MathL.CeilToInt(bounds.Height));
            img.Mutate(x => x.DrawText(m_text, font, Rgba32.White, new PointF(-bounds.X, -bounds.Y)));

            for (int x = 0; x < img.Width; x++)
                for (int y = 0; y < img.Height; y++)
                    img[x, y] = new Rgba32(255, 255, 255, img[x, y].A);

            m_texture.Create2DFromImage(img);

            IsDirty = false;
        }
    }
}
