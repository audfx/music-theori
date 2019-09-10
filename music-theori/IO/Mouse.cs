using System;
using System.Collections.Generic;
using System.Numerics;

using static theori.Platform.SDL.SDL;

namespace theori.IO
{
    public interface IMouseListener
    {
        bool MouseButtonPressed(MouseButtonInfo info);
        bool MouseButtonReleased(MouseButtonInfo info);

        bool MouseWheelScrolled(int x, int y);

        bool MouseMoved(int x, int y, int dx, int dy);
    }

    public static class Mouse
    {
        internal static int x, y;
        internal static int sx, sy;

        internal static bool relative;

        public static int X => x;
        public static int Y => y;

        public static Vector2 Position => new Vector2(x, y);
        
        public static event Action<MouseButtonInfo> ButtonPress = button => listeners.ForEach(l => l.MouseButtonPressed(button));
        public static event Action<MouseButtonInfo> ButtonRelease = button => listeners.ForEach(l => l.MouseButtonReleased(button));

        public static event Action<int, int, int, int> Move = (x, y, dx, dy) => listeners.ForEach(l => l.MouseMoved(x, y, dx, dy));
        public static event Action<int, int> Scroll = (x, y) => listeners.ForEach(l => l.MouseWheelScrolled(x, y));

        private static readonly HashSet<MouseButton> lastHeldButtons = new HashSet<MouseButton>();
        private static readonly HashSet<MouseButton> heldButtons = new HashSet<MouseButton>();

        private static readonly List<IMouseListener> listeners = new List<IMouseListener>();

        public static bool Relative
        {
            get => relative;
            set => SDL_SetRelativeMouseMode((relative = value) ? SDL_bool.SDL_TRUE : SDL_bool.SDL_FALSE);
        }

        internal static void Update()
        {
            lastHeldButtons.Clear();
            foreach (var button in heldButtons)
                lastHeldButtons.Add(button);
        }
        
        public static bool IsDown(MouseButton button) => heldButtons.Contains(button);
        public static bool IsUp(MouseButton button) => !heldButtons.Contains(button);
        
        internal static void InvokePress(MouseButton button)
        {
            heldButtons.Add(button);
            ButtonPress(new MouseButtonInfo()
            {
                Button = button,
            });
        }

        internal static void InvokeRelease(MouseButton button)
        {
            heldButtons.Remove(button);
            ButtonRelease(new MouseButtonInfo()
            {
                Button = button,
            });
        }

        internal static void InvokeMove(int mx, int my, int dx, int dy)
        {
            Move(x = mx, y = my, dx, dy);
        }

        internal static void InvokeScroll(int x, int y)
        {
            Scroll(sx = x, sy = y);
        }
    }
}
