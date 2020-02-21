using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace theori.Configuration
{
    public sealed class NewConfigEntry
    {
        public readonly Type GroupType;
        public ConfigGroupAttribute GroupAttrib;

        public string? Namespace => GroupAttrib.GroupName;

        public readonly PropertyInfo Property;
        public readonly ConfigAttribute ConfigAttrib;

        public Type Type => Property.PropertyType;
        public string? Section => ConfigAttrib.Section;
        public string Name => ConfigAttrib.Name ?? Property.Name.CamelStringToSeparated();

        public object Value
        {
            get => Property.GetMethod.Invoke(null, null);
            set => Property.SetMethod.Invoke(null, new[] { Convert.ChangeType(value, Property.PropertyType) });
        }

        public NewConfigEntry(Type groupType, ConfigGroupAttribute groupAttrib, PropertyInfo property, ConfigAttribute configAttrib)
        {
            GroupType = groupType;
            GroupAttrib = groupAttrib;

            Property = property;
            ConfigAttrib = configAttrib;
        }
    }

    public static class UserConfigManager
    {
        private static Dictionary<string, (Type, ConfigGroupAttribute)>? settingsTypes;
        private static Dictionary<string, (Type, ConfigGroupAttribute)> SettingsTypes
        {
            get
            {
                return settingsTypes ?? (settingsTypes = Gather());

                static Dictionary<string, (Type, ConfigGroupAttribute)> Gather()
                {
                    var result = new Dictionary<string, (Type, ConfigGroupAttribute)>();
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var type in asm.GetTypes())
                        {
                            var attrib = type.GetCustomAttribute<ConfigGroupAttribute>();
                            if (attrib == null) continue;

                            result[attrib.GroupName] = (type, attrib);
                        }
                    }
                    return result;
                }
            }
        }

        private static Dictionary<string, NewConfigEntry>? configEntries;
        public static Dictionary<string, NewConfigEntry> ConfigEntries
        {
            get
            {
                return configEntries ?? (configEntries = Gather());

                static Dictionary<string, NewConfigEntry> Gather()
                {
                    var result = new Dictionary<string, NewConfigEntry>();
                    foreach (var (groupName, (type, typeAttrib)) in SettingsTypes)
                    {
                        foreach (var property in type.GetProperties())
                        {
                            if (property.CanWrite && property.CanWrite && property.GetCustomAttribute<ConfigAttribute>() is ConfigAttribute attrib)
                            {
                                var entry = new NewConfigEntry(type, typeAttrib, property, attrib);
                                result[ConfigKey(groupName, property.Name)] = entry;
                            }
                        }
                    }
                    return result;
                }
            }
        }

        private static readonly Dictionary<string, object> dynamicConfig = new Dictionary<string, object>();

        private static string ConfigKey(string groupName, string configName) => $"{groupName}.{configName}";

        private static object ConvertValue(dynamic jObjectDyn, Type typeHint)
        {
            if (typeHint == typeof(bool)) return (bool)jObjectDyn;
            else if (typeHint == typeof(sbyte)) return (sbyte)jObjectDyn;
            else if (typeHint == typeof(short)) return (short)jObjectDyn;
            else if (typeHint == typeof(int)) return (int)jObjectDyn;
            else if (typeHint == typeof(long)) return (long)jObjectDyn;
            else if (typeHint == typeof(byte)) return (byte)jObjectDyn;
            else if (typeHint == typeof(ushort)) return (ushort)jObjectDyn;
            else if (typeHint == typeof(uint)) return (uint)jObjectDyn;
            else if (typeHint == typeof(ulong)) return (ulong)jObjectDyn;
            else if (typeHint == typeof(float)) return (float)jObjectDyn;
            else if (typeHint == typeof(double)) return (double)jObjectDyn;
            else if (typeHint == typeof(decimal)) return (decimal)jObjectDyn;
            else if (typeHint == typeof(string)) return (string)jObjectDyn;
            else if (typeHint == typeof(char)) return (char)jObjectDyn;
            else if (typeHint.IsEnum) return Enum.Parse(typeHint, (string)jObjectDyn);

            throw new ArgumentException();
        }

        private static object GetObjectValue(JToken jToken)
        {
            if (jToken.Type == JTokenType.Boolean) return jToken.ToObject<bool>();
            else if (jToken.Type == JTokenType.Integer) return jToken.ToObject<long>();
            else if (jToken.Type == JTokenType.Float) return jToken.ToObject<double>();
            else if (jToken.Type == JTokenType.String) return jToken.ToObject<string>();

            throw new ArgumentException();
        }

        /// <summary>
        /// Returns false if a save file was not found, true otherwise.
        /// </summary>
        public static bool LoadFromFile(string? configFileName = null)
        {
            string fileName = configFileName ?? "default-config.json";
            if (!File.Exists(fileName))
            {
                SaveToFile(fileName);
                return false;
            }

            using var reader = new JsonTextReader(new StreamReader(File.OpenRead(fileName)));

            var groups = JArray.Load(reader);
            foreach (var groupToken in groups)
            {
                if (!(groupToken is JObject group)) continue;

                if (group.ContainsKey("entries") && group["entries"] is JObject entries)
                {
                    if (!group.ContainsKey("name")) continue;
                    string groupName = group["name"].ToObject<string>();

                    if (string.IsNullOrEmpty(groupName))
                    {
                        foreach (var (entryName, entryValue) in entries)
                            dynamicConfig[entryName] = GetObjectValue(entryValue);
                    }
                    else
                    {
                        foreach (var (entryName, entryValue) in entries)
                        {
                            if (ConfigEntries.TryGetValue(ConfigKey(groupName, entryName), out var entry))
                                entry.Value = ConvertValue(entryValue, entry.Type);
                        }
                    }
                }
            }

            return true;
        }
        
        public static void SaveToFile(string? configFileName = null)
        {
            string fileName = configFileName ?? "default-config.json";

            var stringWriter = new StringWriter();
            using var writer = new JsonTextWriter(stringWriter);

            writer.WriteStartArray();
            {
                if (dynamicConfig.Count > 0)
                {
                    writer.WriteStartObject();
                    {
                        writer.WritePropertyName("name");
                        writer.WriteValue("");

                        writer.WritePropertyName("entries");
                        writer.WriteStartObject();
                        {
                            foreach (var (key, value) in dynamicConfig)
                            {
                                writer.WritePropertyName(key);
                                writer.WriteValue(value);
                            }
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                }

                foreach (var (groupName, (type, _)) in SettingsTypes)
                {
                    writer.WriteStartObject();
                    {
                        writer.WritePropertyName("name");
                        writer.WriteValue(groupName);

                        writer.WritePropertyName("entries");
                        writer.WriteStartObject();
                        {
                            foreach (var property in type.GetProperties())
                            {
                                string key = ConfigKey(groupName, property.Name);
                                if (ConfigEntries.TryGetValue(key, out var entry))
                                {
                                    writer.WritePropertyName(property.Name);
                                    WriteValue(entry.Value);
                                }
                            }
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();

            void WriteValue(object value)
            {
                if (value is null)
                    writer.WriteNull();
                else
                {
                    var type = value.GetType();
                    if (type.IsEnum)
                        writer.WriteValue(value.ToString());
                    else writer.WriteValue(value);
                }
            }

            writer.Flush();
            File.WriteAllText(fileName, stringWriter.ToString().FormatJson());
        }

        internal static object? GetFromKey(string key)
        {
            if (GetDynamic(key) is object value) return value;
            if (ConfigEntries.TryGetValue(key, out var entry)) return entry.Value;
            return null;
        }

        internal static void SetFromKey(string key, object value)
        {
            if (ConfigEntries.TryGetValue(key, out var entry))
                entry.Value = value;
            else SetDynamic(key, value);
        }

        internal static object? GetDynamic(string key) => dynamicConfig.TryGetValue(key, out var result) ? result : null;
        internal static void SetDynamic(string key, object value) => dynamicConfig[key] = value;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ConfigAttribute : Attribute
    {
        public string? Section { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ConfigGroupAttribute : Attribute
    {
        /// <summary>
        /// Determines what namespace to store the config entries in.
        /// By default, if unspecified, this will be the name of the annotated class.
        /// </summary>
        public string GroupName { get; }

        public ConfigGroupAttribute(string groupName)
        {
            GroupName = groupName;
        }
    }
}
