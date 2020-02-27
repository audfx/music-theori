using theori.Graphics.OpenGL;

namespace theori.Graphics
{
    public class Drawable3D
    {
        public Mesh Mesh = new Mesh();
        public Texture Texture = Texture.Empty;

        public Material Material = Material.CreateUninitialized();
        public MaterialParams Params = new MaterialParams();

        public void DrawToQueue(RenderQueue queue, Transform transform)
        {
            Params["MainTexture"] = Texture;
            queue.Draw(transform, Mesh, Material, Params);
        }
    }
}
