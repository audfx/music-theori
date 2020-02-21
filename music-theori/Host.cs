using System;

using theori.Platform;
using theori.Resources;

namespace theori
{
    public static class Host
    {
        private static ClientResourceManager? staticResources = null;

        public static ClientResourceManager StaticResources => staticResources ?? throw new InvalidOperationException();

        public static ClientHost GetSuitableHost(ClientResourceLocator? staticResourceLocator = null)
        {
            staticResources = new ClientResourceManager(staticResourceLocator ?? ClientResourceLocator.Default);

            // TODO(local): switch on things, get better hosts or error when can't get one.
            return new DesktopClientHost();
        }
    }
}
