using theori.Graphics;

namespace theori.BootLoaders
{
    public sealed class SharedBootLoader : Layer
    {
        private readonly BasicSpriteRenderer m_renderer = new BasicSpriteRenderer();

        public SharedBootLoader(string[] args)
        {
        }

        public override void Render()
        {
            m_renderer.BeginFrame();
            m_renderer.Write("Shared Boot Loader", 10, 10);
            m_renderer.EndFrame();
        }
    }
}
