namespace System
{
    public struct Cached<T>
    {
        public delegate T PropertyUpdater();
        
        public static implicit operator T(Cached<T> value) => value.Value;

        private PropertyUpdater m_updateDelegate;

        private T m_value;
        public T Value
        {
            get
            {
                if (!IsValid)
                    MakeValidOrDefault();
                return m_value;
            }
        }

        public bool IsValid { get; private set; }

        /// <summary>
        /// Refresh this cached object with a custom delegate.
        /// </summary>
        /// <param name="providedDelegate"></param>
        public T Refresh(PropertyUpdater providedDelegate)
        {
            m_updateDelegate = m_updateDelegate ?? providedDelegate;
            return MakeValidOrDefault();
        }

        /// <summary>
        /// Refresh this property.
        /// </summary>
        public T MakeValidOrDefault()
        {
            if (IsValid) return m_value;
            return EnsureValid() ? m_value : default;
        }

        /// <summary>
        /// Refresh using a cached delegate.
        /// </summary>
        /// <returns>Whether refreshing was possible.</returns>
        public bool EnsureValid()
        {
            if (IsValid) return true;

            if (m_updateDelegate == null)
                return false;

            m_value = m_updateDelegate();
            IsValid = true;

            return true;
        }

        /// <summary>
        /// Invalidate the cache of this object.
        /// </summary>
        /// <returns>True if we invalidated from a valid state.</returns>
        public bool Invalidate()
        {
            if (IsValid)
            {
                IsValid = false;
                return true;
            }

            return false;
        }
    }
}
