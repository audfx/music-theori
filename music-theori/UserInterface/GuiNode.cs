using System;

namespace theori.UserInterface
{
    [Flags]
    public enum Invalidation
    {
        None = 0,

        Position = 0x01,

        All = Position,
    }

    public abstract class GuiNode
    {
        private Container? m_parentNode = null;
        private Anchor m_anchor = Anchor.TopLeft;

        public Container? Parent
        {
            get => m_parentNode;
            set
            {
                if (value == m_parentNode)
                    return;

                m_parentNode?.RemoveChild(this);
                m_parentNode = value;
                m_parentNode?.AddChild(this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public Anchor Anchor
        {
            get => m_anchor;
            set
            {
                if (value == m_anchor)
                    return;

                m_anchor = value;
                Invalidate();
            }
        }

        public virtual void Invalidate(Invalidation inv = Invalidation.All)
        {
        }
    }
}
