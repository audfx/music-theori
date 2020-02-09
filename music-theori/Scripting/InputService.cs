using MoonSharp.Interpreter;

using theori.IO;

namespace theori.Scripting
{
    public sealed class InputService : LuaService
    {
        static InputService()
        {
            UserData.RegisterType<KeyCode>();
            UserData.RegisterType<MouseButton>();
            UserData.RegisterType<Axis>();

            UserData.RegisterType<Gamepad>();
            UserData.RegisterType<Controller>();

            UserData.RegisterType<InputService>();
        }

        public readonly LuaBindableEvent KeyPress;
        public readonly LuaBindableEvent KeyRelease;

        public readonly LuaBindableEvent MousePress;
        public readonly LuaBindableEvent MouseRelease;
        public readonly LuaBindableEvent MouseMove;
        public readonly LuaBindableEvent MouseScroll;

        public readonly LuaBindableEvent GamepadConnect;
        public readonly LuaBindableEvent GamepadDisconnect;
        public readonly LuaBindableEvent GamepadPress;
        public readonly LuaBindableEvent GamepadRelease;
        public readonly LuaBindableEvent GamepadAxisChange;

        public readonly LuaBindableEvent ControllerAdd;
        public readonly LuaBindableEvent ControllerRemove;
        public readonly LuaBindableEvent ControllerPress;
        public readonly LuaBindableEvent ControllerRelease;
        public readonly LuaBindableEvent ControllerAxisChange;
        public readonly LuaBindableEvent ControllerAxisTick;

        public InputService(ExecutionEnvironment env)
            : base(env)
        {
            KeyPress = new LuaBindableEvent(L);
            KeyRelease = new LuaBindableEvent(L);

            MousePress = new LuaBindableEvent(L);
            MouseRelease = new LuaBindableEvent(L);
            MouseMove = new LuaBindableEvent(L);
            MouseScroll = new LuaBindableEvent(L);

            GamepadConnect = new LuaBindableEvent(L);
            GamepadDisconnect = new LuaBindableEvent(L);
            GamepadPress = new LuaBindableEvent(L);
            GamepadRelease = new LuaBindableEvent(L);
            GamepadAxisChange = new LuaBindableEvent(L);

            ControllerAdd = new LuaBindableEvent(L);
            ControllerRemove = new LuaBindableEvent(L);
            ControllerPress = new LuaBindableEvent(L);
            ControllerRelease = new LuaBindableEvent(L);
            ControllerAxisChange = new LuaBindableEvent(L);
            ControllerAxisTick = new LuaBindableEvent(L);
    }

        internal void OnKeyPressed(KeyInfo keyInfo) => KeyPress.Fire(keyInfo.KeyCode);
        internal void OnKeyReleased(KeyInfo keyInfo) => KeyRelease.Fire(keyInfo.KeyCode);

        internal void OnMousePressed(MouseButtonInfo buttonInfo) => MousePress.Fire(buttonInfo.Button, UserInputService.MouseX, UserInputService.MouseY);
        internal void OnMouseReleased(MouseButtonInfo buttonInfo) => MouseRelease.Fire(buttonInfo.Button, UserInputService.MouseX, UserInputService.MouseY);
        internal void OnMouseMoved(int x, int y, int dx, int dy) => MouseMove.Fire(x, y, dx, dy);
        internal void OnMouseScrolled(int dx, int dy) => MouseScroll.Fire(dx, dy);

        internal void OnGamepadConnected(Gamepad gamepad) => GamepadConnect.Fire(gamepad);
        internal void OnGamepadDisconnected(Gamepad gamepad) => GamepadDisconnect.Fire(gamepad);
        internal void OnGamepadPressed(GamepadButtonInfo buttonInfo) => GamepadPress.Fire(buttonInfo.Gamepad, buttonInfo.Button);
        internal void OnGamepadReleased(GamepadButtonInfo buttonInfo) => GamepadRelease.Fire(buttonInfo.Gamepad, buttonInfo.Button);
        internal void OnGamepadAxisChanged(GamepadAxisInfo axisInfo) => GamepadAxisChange.Fire(axisInfo.Gamepad, axisInfo.Axis, axisInfo.Value);

        internal void OnControllerAdded(Controller gamepad) => ControllerAdd.Fire(gamepad);
        internal void OnControllerRemoved(Controller gamepad) => ControllerRemove.Fire(gamepad);
        internal void OnControllerPressed(ControllerButtonInfo buttonInfo) => ControllerPress.Fire(buttonInfo.Controller, buttonInfo.Button);
        internal void OnControllerReleased(ControllerButtonInfo buttonInfo) => ControllerRelease.Fire(buttonInfo.Controller, buttonInfo.Button);
        internal void OnControllerAxisChanged(ControllerAxisInfo axisInfo) => ControllerAxisChange.Fire(axisInfo.Controller, axisInfo.Axis, axisInfo.Value);
        internal void OnControllerAxisTicked(ControllerAxisTickInfo axisTickInfo) => ControllerAxisChange.Fire(axisTickInfo.Controller, axisTickInfo.Axis, axisTickInfo.Direction);
    }
}
