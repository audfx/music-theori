using System;
using System.Collections.Generic;
using System.Text;

namespace theori.UserInterface
{
    public class Container : GuiNode
    {
        private readonly HashSet<GuiNode> m_children = new HashSet<GuiNode>();

        public IEnumerable<GuiNode> Children
        {
            get => m_children;
            set
            {
                Clear();
                AddChildren(value);
            }
        }

        public void Clear()
        {
            foreach (var child in m_children)
                child.Parent = null;
            m_children.Clear();
        }

        public void AddChildren(IEnumerable<GuiNode> children)
        {
            foreach (var child in children)
                child.Parent = this;
        }

        /// <summary>
        /// Adds the given child to this container.
        /// </summary>
        /// <param name="child">The child to add.</param>
        /// <returns>True if the child is added to this container, false if it already existed.</returns>
        public bool AddChild(GuiNode child)
        {
            bool result = m_children.Add(child);
            child.Invalidate();
            return result;
        }

        public void RemoveChild(GuiNode child) => m_children.Remove(child);
    }
}
