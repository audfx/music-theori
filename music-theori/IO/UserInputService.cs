using System;
using System.Collections.Generic;
using System.Numerics;
using static theori.Platform.SDL.SDL;

namespace theori.IO
{
    public interface IKeyboardListener
    {
        bool KeyPressed(KeyInfo info);
        bool KeyReleased(KeyInfo info);
    }

    public interface IMouseListener
    {
        bool MouseButtonPressed(MouseButtonInfo info);
        bool MouseButtonReleased(MouseButtonInfo info);

        bool MouseWheelScrolled(int x, int y);

        bool MouseMoved(int x, int y, int dx, int dy);
    }

    public static class UserInputService
    {
        internal const int SCANCODE_MASK = 1 << 30;

        [Flags]
        public enum Modes : byte
        {
            None = 0,

            Desktop = 0x01,
            Gamepad = 0x02,

            /// <summary>
            /// Controllers in :theori are abstract models of a specific layout.
            /// A Controller can be made up of any combination of keyboard, mouse
            ///  and gamepad inputs in various configurations.
            /// If a Controller would accept input, it will block input to the Desktop and Gamepad modes.
            /// </summary>
            Controller = 0x04,

            Any = Desktop | Gamepad | Controller,
        }

        private static LayerStack? layerStack;

        public static int MouseX { get; internal set; }
        public static int MouseY { get; internal set; }
        public static int ScrollX { get; internal set; }
        public static int ScrollY { get; internal set; }
        
        public static Vector2 MousePosition => new Vector2(MouseX, MouseY);

        private static bool isMouseRelative;
        public static bool MouseRelative
        {
            get => isMouseRelative;
            set
            {
                isMouseRelative = value;
                SetDesiredMouseGrabbedStatus();
            }
        }

        public static event Action<KeyInfo> KeyPressed = info => layerStack?.KeyPressed(info);
        public static event Action<KeyInfo> KeyReleased = info => layerStack?.KeyReleased(info);
        public static event Action<KeyInfo>? RawKeyPressed;
        public static event Action<KeyInfo>? RawKeyReleased;

        public static event Action<MouseButtonInfo> MouseButtonPressed = info => layerStack?.MouseButtonPressed(info);
        public static event Action<MouseButtonInfo> MouseButtonReleased = info => layerStack?.MouseButtonReleased(info);
        public static event Action<int, int, int, int> MouseMoved = (x, y, dx, dy) => layerStack?.MouseMoved(x, y, dx, dy);
        public static event Action<int, int> MouseWheelScrolled = (dx, dy) => layerStack?.MouseWheelScrolled(dx, dy);
        public static event Action<MouseButtonInfo>? RawMouseButtonPressed;
        public static event Action<MouseButtonInfo>? RawMouseButtonReleased;
        public static event Action<int, int, int, int>? RawMouseMoved;
        public static event Action<int, int>? RawMouseScrolled;

        public static event Action<Gamepad> GamepadConnected = gamepad => layerStack?.GamepadConnected(gamepad);
        public static event Action<Gamepad> GamepadDisconnected = gamepad => layerStack?.GamepadDisconnected(gamepad);
        public static event Action<GamepadButtonInfo> GamepadButtonPressed = info => layerStack?.GamepadButtonPressed(info);
        public static event Action<GamepadButtonInfo> GamepadButtonReleased = info => layerStack?.GamepadButtonReleased(info);
        public static event Action<GamepadAxisInfo> GamepadAxisChanged = info => layerStack?.GamepadAxisChanged(info);
        public static event Action<GamepadBallInfo> GamepadBallChanged = info => layerStack?.GamepadBallChanged(info);
        public static event Action<GamepadButtonInfo>? RawGamepadButtonPressed;
        public static event Action<GamepadButtonInfo>? RawGamepadButtonReleased;
        public static event Action<GamepadAxisInfo>? RawGamepadAxisChanged;
        public static event Action<GamepadBallInfo>? RawGamepadBallChanged;
        // TODO(local): gamepad hats, or are those "buttons" to us eventually?

        public static event Action<Controller> ControllerAdded = gamepad => layerStack?.ControllerAdded(gamepad);
        public static event Action<Controller> ControllerRemoved = gamepad => layerStack?.ControllerRemoved(gamepad);
        public static event Action<ControllerButtonInfo> ControllerButtonPressed = info => layerStack?.ControllerButtonPressed(info);
        public static event Action<ControllerButtonInfo> ControllerButtonReleased = info => layerStack?.ControllerButtonReleased(info);
        public static event Action<ControllerAxisInfo> ControllerAxisChanged = info => layerStack?.ControllerAxisChanged(info);
        public static event Action<ControllerAxisTickInfo> ControllerAxisTicked = info => layerStack?.ControllerAxisTicked(info);

        private static readonly HashSet<KeyCode> lastHeldKeys = new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();

        private static readonly HashSet<MouseButton> lastHeldMouseButtons = new HashSet<MouseButton>();
        private static readonly HashSet<MouseButton> heldMouseButtons = new HashSet<MouseButton>();

        private static readonly Dictionary<int, Gamepad> gamepads = new Dictionary<int, Gamepad>();
        private static readonly HashSet<Controller> controllers = new HashSet<Controller>();

        public static IEnumerable<Gamepad> ConnectedGamepads => gamepads.Values;

        public static bool ControllersRequireMouseGrabbed
        {
            get
            {
                foreach (var c in controllers)
                {
                    if (c.RequiresMouseGrabbed)
                        return true;
                }
                return false;
            }
        }

        public static Gamepad? TryGetGamepadFromDeviceIndex(int deviceIndex)
        {
            if (gamepads.TryGetValue(deviceIndex, out var gamepad))
                return gamepad;
            return null;
        }

        private static Gamepad? TryGetGamepadFromInstanceId(int instanceId)
        {
            foreach (var (deviceIndex, gamepad) in gamepads)
            {
                if (gamepad.InstanceId == instanceId)
                    return gamepad;
            }

            return null;
        }

        public static Modes InputModes { get; private set; } = Modes.Any;

        public static void Initialize(LayerStack layerStack)
        {
            using var _ = Profiler.Scope("UserInputService::Initialize");

            SDL_InitSubSystem(SDL_INIT_JOYSTICK);
            UserInputService.layerStack = layerStack;
        }

        public static void SetInputMode(Modes modes)
        {
            if (modes == InputModes) return;

            // if leaving desktop mode, unpress all keys and mouse buttons
            if (InputModes.HasFlag(Modes.Desktop) && !modes.HasFlag(Modes.Desktop))
            {
                foreach (var key in heldKeys)
                    KeyReleased(new KeyInfo() { KeyCode = key });
                foreach (var button in heldMouseButtons)
                    MouseButtonReleased(new MouseButtonInfo() { Button = button });
            }

            if (InputModes.HasFlag(Modes.Gamepad) && !modes.HasFlag(Modes.Gamepad))
            {
                foreach (var (deviceIndex, gamepad) in gamepads)
                {
                    foreach (uint button in gamepad.GetAllHeldButtons())
                        GamepadButtonReleased(new GamepadButtonInfo() { Gamepad = gamepad, Button = button });
                }
            }

            if (InputModes.HasFlag(Modes.Controller) && !modes.HasFlag(Modes.Controller))
            {
                foreach (var controller in controllers)
                {
                    foreach (var button in controller.GetAllHeldButtons())
                        ControllerButtonReleased(new ControllerButtonInfo() { Controller = controller, Button = button });
                }
            }

            InputModes = modes;
        }

        private static void SetDesiredMouseGrabbedStatus()
        {
            static bool GetDesiredMouseGrabbedStatus()
            {
                if (InputModes.HasFlag(Modes.Controller))
                {
                    if (ControllersRequireMouseGrabbed)
                        return true;
                }

                // For now, assume mouse relative even if desktop mode isn't enabled.
                // bc really, when mouse is grabbed it's for a reason; desktop mode or otherwise.

                return MouseRelative;
            }

            SDL_SetRelativeMouseMode(GetDesiredMouseGrabbedStatus() ? SDL_bool.SDL_TRUE : SDL_bool.SDL_FALSE);
        }

        public static bool RegisterController(Controller controller)
        {
            if (controllers.Contains(controller)) return false;
            if (controller.RequiresMouseGrabbed && ControllersRequireMouseGrabbed)
                return false;

            controllers.Add(controller);

            SetDesiredMouseGrabbedStatus();

            controller.Pressed += Controller_Pressed;
            controller.Released += Controller_Released;
            controller.AxisChanged += Controller_AxisChanged;
            controller.AxisTicked += Controller_AxisTicked;

            ControllerAdded(controller);

            return true;
        }

        public static void RemoveController(Controller controller)
        {
            if (!controllers.Contains(controller)) return;

            controllers.Remove(controller);

            SetDesiredMouseGrabbedStatus();

            controller.Pressed -= Controller_Pressed;
            controller.Released -= Controller_Released;
            controller.AxisChanged -= Controller_AxisChanged;
            controller.AxisTicked -= Controller_AxisTicked;

            ControllerRemoved(controller);
        }

        private static void Controller_Pressed(Controller controller, HybridLabel label)
        {
            if (!InputModes.HasFlag(Modes.Controller)) return;
            ControllerButtonPressed(new ControllerButtonInfo() { Controller = controller, Button = label });
        }

        private static void Controller_Released(Controller controller, HybridLabel label)
        {
            if (!InputModes.HasFlag(Modes.Controller)) return;
            ControllerButtonReleased(new ControllerButtonInfo() { Controller = controller, Button = label });
        }

        private static void Controller_AxisChanged(Controller controller, HybridLabel label, float value, float delta)
        {
            if (!InputModes.HasFlag(Modes.Controller)) return;
            ControllerAxisChanged(new ControllerAxisInfo() { Controller = controller, Axis = label, Value = value, Delta = delta });
        }

        private static void Controller_AxisTicked(Controller controller, HybridLabel label, int direction)
        {
            if (!InputModes.HasFlag(Modes.Controller)) return;
            ControllerAxisTicked(new ControllerAxisTickInfo() { Controller = controller, Axis = label, Direction = direction });
        }

        internal static void Update()
        {
            using var _ = Profiler.Scope("UserInputService::Update");

            lastHeldKeys.Clear();
            foreach (var key in heldKeys)
                lastHeldKeys.Add(key);

            lastHeldMouseButtons.Clear();
            foreach (var button in heldMouseButtons)
                lastHeldMouseButtons.Add(button);

            foreach (var (deviceIndex, gamepad) in gamepads)
                gamepad.Update();

            if (InputModes.HasFlag(Modes.Controller))
            {
                foreach (var controller in controllers)
                    controller.Update(Time.Delta);
            }
        }

        public static KeyCode ToKeyCode(ScanCode code) => (KeyCode)((int)code | SCANCODE_MASK);

        public static bool IsKeyDown(KeyCode key) => heldKeys.Contains(key);
        public static bool IsKeyUp(KeyCode key) => !heldKeys.Contains(key);

        internal static void Keyboard_KeyPress(KeyInfo info)
        {
            Profiler.Instant("UserInputService::Keyboard_KeyPress", "p", ("key", info.KeyCode.ToString()));

            bool pressed = heldKeys.Add(info.KeyCode);
            if (!pressed) return;

            RawKeyPressed?.Invoke(info);

            if (InputModes.HasFlag(Modes.Controller))
            {
                foreach (var controller in controllers)
                {
                    if (controller.Keyboard_KeyPress(info))
                        return; // don't propogate
                }
            }

            if (InputModes.HasFlag(Modes.Desktop))
                KeyPressed(info);
        }

        internal static void Keyboard_KeyRelease(KeyInfo info)
        {
            Profiler.Instant("UserInputService::Keyboard_KeyRelease", "p", ("key", info.KeyCode.ToString()));

            bool released = heldKeys.Remove(info.KeyCode);
            if (!released) return;

            RawKeyReleased?.Invoke(info);

            if (InputModes.HasFlag(Modes.Controller))
            {
                foreach (var controller in controllers)
                {
                    if (controller.Keyboard_KeyRelease(info))
                        return;
                }
            }

            if (InputModes.HasFlag(Modes.Desktop))
                KeyReleased(info);
        }

        internal static void Mouse_MouseButtonPress(MouseButtonInfo info)
        {
            Profiler.Instant("UserInputService::Mouse_MouseButtonPress", "p", ("button", info.Button.ToString()));

            bool pressed = heldMouseButtons.Add(info.Button);
            if (!pressed) return;

            RawMouseButtonPressed?.Invoke(info);

            if (InputModes.HasFlag(Modes.Controller))
            {
                foreach (var controller in controllers)
                {
                    if (controller.Mouse_MouseButtonPress(info))
                        return;
                }
            }

            if (InputModes.HasFlag(Modes.Desktop))
                MouseButtonPressed(info);
        }

        internal static void Mouse_MouseButtonRelease(MouseButtonInfo info)
        {
            Profiler.Instant("UserInputService::Mouse_MouseButtonRelease", "p", ("button", info.Button.ToString()));

            bool released = heldMouseButtons.Remove(info.Button);
            if (!released) return;

            RawMouseButtonReleased?.Invoke(info);

            if (InputModes.HasFlag(Modes.Controller))
            {
                foreach (var controller in controllers)
                {
                    if (controller.Mouse_MouseButtonRelease(info))
                        return;
                }
            }

            if (InputModes.HasFlag(Modes.Desktop))
                MouseButtonReleased(info);
        }

        internal static void Mouse_Move(int x, int y, int dx, int dy)
        {
            RawMouseMoved?.Invoke(x, y, dx, dy);

            if (InputModes.HasFlag(Modes.Controller))
            {
                foreach (var controller in controllers)
                {
                    if (controller.Mouse_MouseMove(x, y, dx, dy))
                        return;
                }
            }

            if (InputModes.HasFlag(Modes.Desktop))
                MouseMoved(x, y, dx, dy);
        }

        internal static void Mouse_Scroll(int dx, int dy)
        {
            Profiler.Instant("UserInputService::Mouse_MouseScroll", "p", ("dx", dx.ToString()), ("dy", dy.ToString()));

            RawMouseScrolled?.Invoke(dx, dy);

            if (InputModes.HasFlag(Modes.Desktop))
                MouseWheelScrolled(dx, dy);
        }

        internal static void Gamepad_Connected(Gamepad gamepad)
        {
            Profiler.Instant("UserInputService::Gamepad_Connected", "p", ("deviceIndex", gamepad.DeviceIndex.ToString()), ("instanceId", gamepad.InstanceId.ToString()));

            gamepads[gamepad.DeviceIndex] = gamepad;
            gamepad.Open();

            GamepadConnected(gamepad);
        }

        internal static void Gamepad_Disconnected(int instanceId)
        {
            if (TryGetGamepadFromInstanceId(instanceId) is { } gamepad)
            {
                Profiler.Instant("UserInputService::Gamepad_Disconnected", "p", ("deviceIndex", gamepad.DeviceIndex.ToString()), ("instanceId", gamepad.InstanceId.ToString()));

                GamepadDisconnected(gamepad);

                gamepad.Close();
                gamepads.Remove(gamepad.DeviceIndex);
            }
        }

        internal static void Gamepad_Pressed(int instanceId, byte button)
        {
            if (TryGetGamepadFromInstanceId(instanceId) is { } gamepad)
            {
                if (!gamepad.Gamepad_ButtonPressed(button)) return;

                Profiler.Instant("UserInputService::Gamepad_Pressed", "p", ("button", button.ToString()), ("deviceIndex", gamepad.DeviceIndex.ToString()), ("instanceId", gamepad.InstanceId.ToString()));

                var info = new GamepadButtonInfo() { Gamepad = gamepad, Button = button };
                RawGamepadButtonPressed?.Invoke(info);

                if (InputModes.HasFlag(Modes.Controller))
                {
                    foreach (var controller in controllers)
                    {
                        if (controller.Gamepad_Pressed(info))
                            return;
                    }
                }

                if (InputModes.HasFlag(Modes.Gamepad))
                    GamepadButtonPressed(info);
            }
        }

        internal static void Gamepad_Released(int instanceId, byte button)
        {
            if (TryGetGamepadFromInstanceId(instanceId) is { } gamepad)
            {
                if (!gamepad.Gamepad_ButtonReleased(button)) return;

                Profiler.Instant("UserInputService::Gamepad_Released", "p", ("button", button.ToString()), ("deviceIndex", gamepad.DeviceIndex.ToString()), ("instanceId", gamepad.InstanceId.ToString()));

                var info = new GamepadButtonInfo() { Gamepad = gamepad, Button = button };
                RawGamepadButtonReleased?.Invoke(info);

                if (InputModes.HasFlag(Modes.Controller))
                {
                    foreach (var controller in controllers)
                    {
                        if (controller.Gamepad_Released(info))
                            return;
                    }
                }

                if (InputModes.HasFlag(Modes.Gamepad))
                    GamepadButtonReleased(info);
            }
        }

        internal static void Gamepad_AxisMotion(int instanceId, byte axis, short axisValue)
        {
            if (TryGetGamepadFromInstanceId(instanceId) is { } gamepad)
            {
                var info = new GamepadAxisInfo() { Gamepad = gamepad, Axis = axis, Value = axisValue / (float)0x7FFF };
                RawGamepadAxisChanged?.Invoke(info);

                if (InputModes.HasFlag(Modes.Controller))
                {
                    foreach (var controller in controllers)
                    {
                        if (controller.Gamepad_AxisMotion(info))
                            return;
                    }
                }

                if (InputModes.HasFlag(Modes.Gamepad))
                    GamepadAxisChanged(info);
            }
        }

        internal static void Gamepad_BallMotion(int instanceId, byte ball, short xrelValue, short yrelValue)
        {
            if (TryGetGamepadFromInstanceId(instanceId) is { } gamepad)
            {
                var info = new GamepadBallInfo() { Gamepad = gamepad, Ball = ball,
                    XRelative = xrelValue / (float)0x7FFF, YRelative = yrelValue / (float)0x7FFF };
                RawGamepadBallChanged?.Invoke(info);

                if (InputModes.HasFlag(Modes.Controller))
                {
                    foreach (var controller in controllers)
                    {
                        if (controller.Gamepad_BallMotion(info))
                            return;
                    }
                }

                if (InputModes.HasFlag(Modes.Gamepad))
                    GamepadBallChanged(info);
            }
        }
    }
}
