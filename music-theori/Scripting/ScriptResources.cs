using theori.Resources;

using MoonSharp.Interpreter;

namespace theori.Scripting
{
    public sealed class ScriptResources : BaseScriptInstance
    {
        public static readonly ScriptResources StaticResources = new ScriptResources(Host.StaticResources);

        [MoonSharpHidden]
        public readonly ClientResourceManager Resources;

        private ScriptResources(ClientResourceManager resources)
        {
            Resources = resources;
        }
    }
}
