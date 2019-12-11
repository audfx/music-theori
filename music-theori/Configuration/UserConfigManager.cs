using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace theori.Configuration
{
    /*

{
    "Static": {
        "theori": {
        },

        "NeuroSonic": {
        }
    },

    "Dynamic": {
    }
}

     */

    public static class UserConfigManager
    {
        public static void LoadFromFile(string configFileName)
        {
            var json = new JsonSerializer();
        }

        public static void SaveToFile(string configFileName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ConfigGroupAttribute : Attribute
    {
        /// <summary>
        /// Determines what namespace to store the config entries in.
        /// By default, if unspecified, this will be the name of the annotated class.
        /// </summary>
        public string? Namespace { get; set; }
    }
}
