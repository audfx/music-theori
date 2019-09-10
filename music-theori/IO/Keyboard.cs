using System;
using System.Collections.Generic;

namespace theori.IO
{
    public interface IKeyboardListener
    {
        bool KeyPressed(KeyInfo info);
        bool KeyReleased(KeyInfo info);
    }
    
    public static class Keyboard
    {
	    internal const int SCANCODE_MASK = 1 << 30;
        
        private static readonly HashSet<KeyCode> lastHeldKeys = new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
        
        public static event Action<KeyInfo> KeyPress = key => listeners.ForEach(l => l.KeyPressed(key));
        public static event Action<KeyInfo> KeyRelease = key => listeners.ForEach(l => l.KeyReleased(key));

        private static readonly List<IKeyboardListener> listeners = new List<IKeyboardListener>();

        public static void AddKeyboardListener(IKeyboardListener listener)
        {
            if (listeners.Contains(listener)) return;
            listeners.Add(listener);
        }

        public static void RemoveKeyboardListener(IKeyboardListener listener)
        {
            listeners.Remove(listener);
        }

        internal static void Update()
        {
            lastHeldKeys.Clear();
            foreach (var button in heldKeys)
                lastHeldKeys.Add(button);
        }

	    public static KeyCode ToKeyCode(ScanCode code) => (KeyCode)((int)code | SCANCODE_MASK);
        
        public static bool IsDown(KeyCode key) => heldKeys.Contains(key);
        public static bool IsUp(KeyCode key) => !heldKeys.Contains(key);
        
        internal static void InvokePress(KeyInfo info)
        {
            if (!heldKeys.Add(info.KeyCode)) return;
            KeyPress?.Invoke(info);
        }

        internal static void InvokeRelease(KeyInfo info)
        {
            bool removed = heldKeys.Remove(info.KeyCode);
            KeyRelease?.Invoke(info);
        }
    }
}
