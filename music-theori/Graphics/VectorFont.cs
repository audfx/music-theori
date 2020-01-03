using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using theori.Graphics.OpenGL;

using SixLabors.Fonts;

using SL_Font = SixLabors.Fonts.Font;
using theori.Resources;

namespace theori.Graphics
{
    public struct VectorGlyphInfo
    {
        public float EmSize, AdvanceWidth;
    }

    public class VectorFont
    {
        public static readonly VectorFont Default;

        static VectorFont()
        {
            Default = new VectorFont(ClientResourceLocator.Default.OpenFileStream("fonts/osaka.unicode.ttf"));
        }

        private readonly SixLabors.Fonts.FontCollection m_collection = new SixLabors.Fonts.FontCollection();
        private readonly FontFamily m_family;
        private readonly SL_Font m_font;

        private readonly Dictionary<int, VectorGlyphInfo> m_infos = new Dictionary<int, VectorGlyphInfo>();
        private readonly Dictionary<int, Path2DCommands> m_paths = new Dictionary<int, Path2DCommands>();

        public VectorFont(string systemFont)
        {
            m_family = SystemFonts.Families.Where(family => family.Name == systemFont).SingleOrDefault() ?? throw new ArgumentException(nameof(systemFont));
            m_font = m_family.CreateFont(64, FontStyle.Regular);
            AddCharacters("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789,<.>/?;:'\"\\|]}[{`~_-=+!@#$%^&^*()");
        }

        public VectorFont(Stream fontStream)
        {
            m_family = m_collection.Install(fontStream);
            m_font = m_family.CreateFont(64, FontStyle.Regular);
            AddCharacters("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789,<.>/?;:'\"\\|]}[{`~_-=+!@#$%^&^*()");
        }

        public bool TryGetGlyphData(char c, out VectorGlyphInfo info, out Path2DCommands cmds)
        {
            AddCharacter(c);

            info = m_infos[c];
            cmds = m_paths[c];

            return true;
        }

        public void AddCharacters(string chars)
        {
            foreach (char c in chars)
                AddCharacter(c);
        }

        public void AddCharacter(char c)
        {
            if (m_infos.ContainsKey(c))
                return;

            var glyph = m_font.GetGlyph(c).Instance;

            var info = new VectorGlyphInfo
            {
                EmSize = glyph.SizeOfEm,
                AdvanceWidth = glyph.AdvanceWidth,
            };

            var cmds = new Path2DCommands();
            (Vector2 Position, bool CurveControl)[] points = glyph.ControlPoints.Zip(glyph.OnCurves, (a, b) => (new Vector2(a.X, -a.Y), !b)).ToArray();

            int segmentStart = 0;
            for (int s = 0; s < glyph.EndPoints.Length; s++)
            {
                int length = (glyph.EndPoints[s] + 1) - segmentStart;
                for (int i = 0; i < length; i++)
                {
                    var (pos, curveControl) = points[i + segmentStart];
                    if (i == 0)
                        cmds.MoveTo(pos);
                    else
                    {
                        if (curveControl)
                        {
                            var (pos2, cc2) = points[++i % length + segmentStart];
                            if (cc2)
                            {
                                var (pos3, cc3) = points[++i % length + segmentStart];
                                cmds.CubicBezierTo(pos, pos2, pos3);
                            }
                            else cmds.QuadraticBezierTo(pos, pos2);
                        }
                        else cmds.LineTo(pos);
                    }
                }
                segmentStart += length;
            }

            cmds.Close();

            m_infos[c] = info;
            m_paths[c] = cmds;
        }
    }
}
