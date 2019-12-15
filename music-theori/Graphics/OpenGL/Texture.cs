using System;
using System.IO;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace theori.Graphics.OpenGL
{
    public sealed class Texture : UIntHandle
    {
        private static Texture? empty;
        public static Texture Empty => empty ?? (empty = CreateEmpty());

        public static Texture FromFile2D(string fileName)
        {
            var texture = new Texture();
            texture.Load2DFromFile(fileName);
            return texture;
        }

        public static Texture FromStream2D(Stream stream)
        {
            var texture = new Texture();
            texture.Load2DFromStream(stream);
            return texture;
        }

        public static Texture CreateUninitialized2D() => new Texture(0, TextureTarget.Texture2D);

        static Texture CreateEmpty()
        {
            var empty = new Texture();
            empty.SetEmpty2D(1, 1);
            empty.Lock();
            return empty;
        }

        public TextureTarget Target = TextureTarget.Texture2D;
        public int Width, Height, Depth;

        public float AspectRatio => (float)Width / Height;

        private TextureFilter m_minFilter = TextureFilter.Linear;
        private TextureFilter m_magFilter = TextureFilter.Linear;

        public TextureFilter MinFilter { get => m_minFilter; set { m_minFilter = value; SetParams(); } }
        public TextureFilter MagFilter { get => m_magFilter; set { m_magFilter = value; SetParams(); } }

        public void Lock() { Locked = true; }
        public bool Locked { get; private set; }

        private Texture(uint handle, TextureTarget target = TextureTarget.Texture2D)
            : base(handle, GL.DeleteTexture)
        {
            Target = target;
        }

        public Texture(TextureTarget target = TextureTarget.Texture2D)
            : base(GL.GenTexture, GL.DeleteTexture)
        {
            Target = target;
            SetParams();
        }

        public void GenerateHandle()
        {
            if (Handle != 0) return;

            Handle = GL.GenTexture();
            SetParams();
        }

        private void SetParams()
        {
            Bind(0);

            GL.TexParameter((uint)Target, GL.GL_TEXTURE_WRAP_S, GL.GL_CLAMP_TO_EDGE);
            GL.TexParameter((uint)Target, GL.GL_TEXTURE_WRAP_T, GL.GL_CLAMP_TO_EDGE);

            GL.TexParameter((uint)Target, GL.GL_TEXTURE_MIN_FILTER, (uint)MinFilter);
            GL.TexParameter((uint)Target, GL.GL_TEXTURE_MAG_FILTER, (uint)MagFilter);
        }

        public void Bind(uint unit)
        {
            if (false && GL.IsExtensionFunctionSupported("glBindTextureUnit"))
                GL.BindTextureUnit(unit, Handle);
            else
            {
			    GL.ActiveTexture(GL.GL_TEXTURE0 + unit);
                GL.BindTexture((uint)Target, Handle);
            }
        }

        public void SetEmpty2D(int width, int height)
        {
            if (Locked) throw new Exception("Cannot direcly modify a locked texture.");

            Target = TextureTarget.Texture2D;

            Bind(0);
            SetParams();

            Width = width;
            Height = height;
            Depth = 0;
            
            byte[] pixels = new byte[Width * Height * 4];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = byte.MaxValue;

            GL.TexImage2D(GL.GL_TEXTURE_2D, 0, GL.GL_RGBA, Width, Height, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, pixels);
        }

        public void Load2DFromStream(Stream stream)
        {
            if (Locked) throw new Exception("Cannot direcly modify a locked texture.");

            using var image = Image.Load(stream);

            unsafe
            {
                var pixels = image.GetPixelSpan();
                fixed (void* bytes = pixels)
                {
                    SetData2D(image.Width, image.Height, new Span<byte>(bytes, pixels.Length));
                }
            }
        }

        public void Load2DFromFile(string fileName)
        {
            if (Locked) throw new Exception("Cannot direcly modify a locked texture.");

            using var image = Image.Load(fileName);

            unsafe
            {
                var pixels = image.GetPixelSpan();
                fixed (void* bytes = pixels)
                {
                    SetData2D(image.Width, image.Height, new Span<byte>(bytes, pixels.Length));
                }
            }
        }

        public void SetData2D(int width, int height, Span<byte> pixelData)
        {
            if (Locked) throw new Exception("Cannot direcly modify a locked texture.");

            Target = TextureTarget.Texture2D;

            Bind(0);
            SetParams();

            Width = width;
            Height = height;
            Depth = 0;

            unsafe
            {
                fixed (void* pin = &MemoryMarshal.GetReference(pixelData))
                {
                    GL.TexImage2D(GL.GL_TEXTURE_2D, 0, GL.GL_RGBA, Width, Height, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, new IntPtr(pin));
                }
            }
        }

        public void SetData2D(int width, int height, byte[] pixelData)
        {
            if (Locked) throw new Exception("Cannot direcly modify a locked texture.");

            Target = TextureTarget.Texture2D;

            Bind(0);
            SetParams();

            Width = width;
            Height = height;
            Depth = 0;

            GL.TexImage2D(GL.GL_TEXTURE_2D, 0, GL.GL_RGBA, Width, Height, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, pixelData);
        }

        internal void Create2DFromImage(Image<Rgba32> image)
        {
            if (Locked) throw new Exception("Cannot direcly modify a locked texture.");

            unsafe
            {
                var pixels = image.GetPixelSpan();
                fixed (void* bytes = pixels)
                {
                    SetData2D(image.Width, image.Height, new Span<byte>(bytes, pixels.Length));
                }
            }
        }
    }
}
