using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using theori.GameModes;
using theori.Scripting;

namespace theori.Charting
{
    public interface IHasEffectDef
    {
        Effects.EffectDef Effect { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class EntityTypeAttribute : Attribute
    {
        public readonly string Name;
        /// <summary>
        /// Used if we support custom serialization for entities.
        /// Needs to be removed otherwise.
        /// </summary>
        public readonly Type SerializerType;

        public EntityTypeAttribute(string name, Type serializerType = null)
        {
            Name = name;
            SerializerType = serializerType;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TheoriPropertyAttribute : Attribute
    {
        public readonly string OverrideName;

        public TheoriPropertyAttribute(string overrideName = null)
        {
            OverrideName = overrideName;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TheoriIgnoreAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TheoriIgnoreDefaultAttribute : Attribute
    {
    }

    [EntityType("Entity")]
    public class Entity : ILinkable<Entity>, IComparable<Entity>, ICloneable
    {
        private static long creationIndexCounter = 0;

        #region Type Registry

        private static readonly Dictionary<string, Type> entityTypesById = new Dictionary<string, Type>();
        private static readonly Dictionary<Type, string> entityIdsByType = new Dictionary<Type, string>();

        public static Type GetEntityTypeById(string id)
        {
            if (entityTypesById.TryGetValue(id, out var type))
                return type;
            return null;
        }

        public static string GetEntityId<T>() where T : Entity => GetEntityIdByType(typeof(T));
        public static string GetEntityIdByType(Type type)
        {
            if (entityIdsByType.TryGetValue(type, out string id))
                return id;
            return null;
        }

        internal static void RegisterTheoriTypes()
        {
            var types = from type in typeof(Entity).Assembly.ExportedTypes
                        where type == typeof(Entity) || type.IsSubclassOf(typeof(Entity))
                        select type;
            RegisterTypes("theori", types);
        }

        public static void RegisterTypesFromGameMode(GameMode gameMode)
        {
            var types = from type in gameMode.GetType().Assembly.ExportedTypes
                        where type.IsSubclassOf(typeof(Entity))
                        select type;
            RegisterTypes(gameMode.Name, types);
        }

        private static void RegisterTypes(string modeName, IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                string typeName;

                var typeAttrib = type.GetCustomAttribute<EntityTypeAttribute>();
                if (typeAttrib != null)
                    typeName = typeAttrib.Name;
                else typeName = type.Name;

                string id = $"{ modeName }.{ typeName }";
                entityTypesById[id] = type;
                entityIdsByType[type] = id;
            }
        }

        #endregion

        static Entity()
        {
            RegisterTheoriTypes();
        }

        private readonly long m_id = ++creationIndexCounter;

        private tick_t m_position, m_duration;

        // Positions (in seconds) calculated from chart timing information.
        private time_t m_calcPosition = long.MinValue, m_calcEndPosition = long.MinValue;

        internal HybridLabel m_lane;

        [MoonSharpVisible(true)]
        public string TypeId => GetEntityIdByType(GetType());

        /// <summary>
        /// The position, in measures, of this entity.
        /// </summary>
        [TheoriProperty("position")]
        [MoonSharpVisible(true)]
        public tick_t Position
        {
            get => m_position;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Entities cannot have negative positions.", nameof(Position));

                if (SetPropertyField(nameof(Position), ref m_position, value))
                    InvalidateTimingCalc();
            }
        }

        [TheoriProperty("duration")]
        [TheoriIgnoreDefault]
        [MoonSharpVisible(true)]
        public tick_t Duration
        {
            get => m_duration;
            set
            {
                if (SetPropertyField(nameof(Duration), ref m_duration, value))
                    InvalidateTimingCalc();
            }
        }

        [MoonSharpVisible(true)]
        public tick_t EndPosition => Position + Duration;

        [MoonSharpVisible(true)]
        public bool IsInstant => m_duration == 0;

        [MoonSharpVisible(true)]
        public time_t AbsolutePosition
        {
            get
            {
                if (Chart == null)
                    throw new InvalidOperationException("Cannot calculate the absolute position of an entity without an assigned Chart.");

                if (m_calcPosition == (time_t)long.MinValue)
                {
                    ControlPoint cp = Chart.ControlPoints.MostRecent(m_position);
                    m_calcPosition = cp.AbsolutePosition + cp.MeasureDuration * (m_position - cp.Position);
                }
                return m_calcPosition;
            }
        }

        [MoonSharpVisible(true)]
        public time_t AbsoluteEndPosition
        {
            get
            {
                if (Chart == null)
                    throw new InvalidOperationException("Cannot calculate the absolute duration of an entity without an assigned Chart.");

                if (m_calcEndPosition == (time_t)long.MinValue)
                {
                    ControlPoint cp = Chart.ControlPoints.MostRecent(EndPosition);
                    m_calcEndPosition = cp.AbsolutePosition + cp.MeasureDuration * (EndPosition - cp.Position);
                }
                return m_calcEndPosition;
            }
        }

        [MoonSharpVisible(true)]
        public time_t AbsoluteDuration => AbsoluteEndPosition - AbsolutePosition;

        [TheoriIgnore]
        [MoonSharpVisible(true)]
        public HybridLabel Lane
        {
            get => m_lane;
            set
            {
                if (m_lane == value) return;

                var chart = Chart;
                if (chart != null)
                {
                    chart[m_lane].Remove(this);
                    // will re-assign Chart and m_stream
                    chart[value].Add(this);
                }
                else m_lane = value;

                OnPropertyChanged(nameof(Lane));
            }
        }

        [MoonSharpVisible(true)]
        public bool HasPrevious => Previous != null;
        [MoonSharpVisible(true)]
        public bool HasNext => Next != null;

        [MoonSharpVisible(true)]
        public Entity Previous => ((ILinkable<Entity>)this).Previous;
        Entity ILinkable<Entity>.Previous { get; set; }

        [MoonSharpVisible(true)]
        public Entity Next => ((ILinkable<Entity>)this).Next;
        Entity ILinkable<Entity>.Next { get; set; }

        [MoonSharpVisible(true)]
        public Entity PreviousConnected
        {
            get
            {
                var p = Previous;
                return p != null && p.EndPosition == Position ? p : null;
            }
        }

        [MoonSharpVisible(true)]
        public Entity NextConnected
        {
            get
            {
                var n = Next;
                return n != null && n.Position == EndPosition ? n : null;
            }
        }

        [MoonSharpHidden]
        public T FirstConnectedOf<T>()
            where T : notnull, Entity
        {
            var current = (T)this;
            while (current?.PreviousConnected is T prev)
                current = prev;
            return current;
        }

        [MoonSharpHidden]
        public T LastConnectedOf<T>()
            where T : notnull, Entity
        {
            var current = (T)this;
            while (current?.NextConnected is T next)
                current = next;
            return current;
        }

        [MoonSharpVisible(true)]
        public Chart Chart { get; internal set; }

        [MoonSharpHidden]
        public Entity()
        {
            ScriptService.RegisterType(GetType());
        }

        [MoonSharpHidden]
        public virtual Entity Clone()
        {
            var result = new Entity()
            {
                m_position = m_position,
                m_duration = m_duration,
                m_lane = m_lane,
            };
            return result;
        }

        object ICloneable.Clone() => Clone();

        public override int GetHashCode() => m_id.GetHashCode();

        [MoonSharpHidden]
        public virtual int CompareTo(Entity other)
        {
            int r = m_position.CompareTo(other.m_position);
            if (r != 0)
                return r;

            if (m_duration == 0)
            {
                if (other.m_duration != 0)
                    return -1;
            }
            else if (other.m_duration == 0)
                return 1;

            // oh well, we tried
            return m_id.CompareTo(other.m_id);
        }
        
        int IComparable<Entity>.CompareTo(Entity other) => CompareTo(other);

        internal void InvalidateTimingCalc()
        {
            m_calcPosition = (time_t)long.MinValue;
            m_calcEndPosition = (time_t)long.MinValue;
        }

        public delegate void PropertyChangedEventHandler(Entity sender, PropertyChangedEventArgs args);

        [Flags]
        public enum Invalidation
        {
            None = 0,
        }

        public sealed class PropertyChangedEventArgs
        {
            public string PropertyName { get; set; }
            public Invalidation Invalidation { get; set; }

            public PropertyChangedEventArgs(string propertyName)
            {
                PropertyName = propertyName;
            }
        }

        [MoonSharpHidden]
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName, Invalidation invalidation = Invalidation.None)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName) { Invalidation = invalidation });
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            PropertyChanged?.Invoke(this, args);
        }

        protected bool SetPropertyField<T>(string propertyName, ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);

            return true;
        }
    }
}
