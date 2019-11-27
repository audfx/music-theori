using System;
using System.Collections.Generic;
using System.Text;

namespace theori.Configuration
{
    public static class ClientConfig
    {
        public static string ConfigFileName { get; private set; } = "theori-client.json";

        public static void Initialize(string? configFileName = null)
        {
            if (configFileName != null)
                ConfigFileName = configFileName;

            LoadFromFile();
        }

        public static void LoadFromFile()
        {
        }

        public static void SaveToFile()
        {
        }

        public static string? GetString(string key) => null;
    }
}
