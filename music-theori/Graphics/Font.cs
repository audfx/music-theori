using System;
using System.Collections.Generic;
using System.IO;

using SixLabors.Fonts;

using theori.Resources;

using SL_Font = SixLabors.Fonts.Font;

namespace theori.Graphics
{
    public sealed class Font
    {
        public static readonly Font Default;

        static Font()
        {
            //Default = new Font("Arial");

            Default = new Font();
            Default.Initialize(ClientResourceLocator.Default.OpenFileStream("fonts/osaka.unicode.ttf"));

            Default.CreateSize(8.0f);
            Default.CreateSize(12.0f);
            Default.CreateSize(16.0f);
            Default.CreateSize(24.0f);
            Default.CreateSize(32.0f);
            Default.CreateSize(48.0f);
            Default.CreateSize(64.0f);
        }

        private readonly FontCollection m_collection = new FontCollection();

        private FontFamily? m_family = null;
        private readonly List<SL_Font> m_fonts = new List<SL_Font>();

        public Font()
        {
        }

        public Font(string systemFont)
        {
            foreach (var family in SystemFonts.Families)
            {
                if (family.Name == systemFont)
                {
                    m_family = family;
                    break;
                }
            }
        }

        public void Initialize(Stream fontStream)
        {
            m_family = m_collection.Install(fontStream);
        }

        public void CreateSize(float size)
        {
            var font = m_family?.CreateFont(size) ?? throw new InvalidOperationException("Font has not been initialized; cannot create a size.");
            m_fonts.Add(font);
        }

        internal SL_Font GetNearestLoadedFont(float size)
        {
            if (m_fonts.Count == 0 || m_family == null)
                throw new InvalidOperationException("Attempt to get a font when no fonts have been loaded.");

            // TODO(local): This should take into account which direction of scaling to prefer, ideally.

            var result = m_fonts[0];
            float nearestAmount = MathL.Abs(result.Size - size);

            for (int i = 1; i < m_fonts.Count; i++)
            {
                var font = m_fonts[i];

                float sizeDist = Math.Abs(font.Size - size);
                if (sizeDist < nearestAmount)
                {
                    nearestAmount = sizeDist;
                    result = font;
                }
            }

            return result;
        }
    }
}
