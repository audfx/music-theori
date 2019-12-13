using System;
using System.Collections.Generic;
using System.Linq;

using theori.Charting;
using theori.Charting.Serialization;

namespace theori.GameModes
{
    public abstract class GameMode
    {
        private static IEnumerable<Type>? m_available;
        public static IEnumerable<Type> GetAvailableGameModeTypes()
        {
            return m_available ?? (m_available = Gather());

            static IEnumerable<Type> Gather()
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in asm.GetTypes())
                    {
                        if (typeof(GameMode).IsAssignableFrom(type))
                            yield return type;
                    }
                }
            }
        }

        private static readonly Dictionary<Type, GameMode> instances = new Dictionary<Type, GameMode>();

        public static GameMode? GetInstance(string name) => instances.Where(p => p.Value.Name == name).Select(p => (GameMode?)p.Value).SingleOrDefault();
        public static GameMode? GetInstance(Type type) => instances.TryGetValue(type, out var result) ? result : null;

        #region Meta Info

        /// <summary>
        /// The name of a gamemode should be unique.
        /// </summary>
        public readonly string Name;

        #endregion

        #region Standalone Mode

        /// <summary>
        /// Whether this game mode supports running as a standalone experience,
        ///  as if it were the only game installed on the client.
        /// When running in standalone, this game mode will be the only
        ///  playable option and has more control over layers.
        /// </summary>
        public virtual bool SupportsStandaloneUsage => false;

        public virtual void InvokeStandalone(string[] args)
        {
            if (SupportsStandaloneUsage)
                throw new System.Exception($"Game Mode \"{ Name }\" is stated to supprt standalone usage, but no standalone initialization is provided.");
            else throw new System.Exception($"Game Mode \"{ Name }\" does not support standalone usage.");
        }

        #endregion

        #region Shared Mode

        /// <summary>
        /// Whether this game mode supports running in the shared-mode client,
        ///  where all shared games can be freely selected.
        /// </summary>
        public virtual bool SupportsSharedUsage => false;

#if false
        public virtual Layer CreateSharedGameLayer()
        {
            if (SupportsSharedUsage)
                throw new System.Exception($"Game Mode \"{ Name }\" is stated to supprt shared usage, but no shared game layer is provided.");
            else throw new System.Exception($"Game Mode \"{ Name }\" does not support shared usage.");
        }
#endif

#endregion

        protected GameMode(string name)
        {
            if (instances.ContainsKey(GetType()))
                throw new InvalidOperationException("Cannot instantiate a game mode multiple times.");
            instances[GetType()] = this;

            Name = name;
        }

        public virtual ChartFactory GetChartFactory() => throw new NotImplementedException();
        public virtual IChartSerializer? CreateChartSerializer(string chartsDirectory, string? fileFormat) => null;
    }
}
