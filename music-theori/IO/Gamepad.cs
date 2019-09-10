using System;
using System.Collections.Generic;
using System.Text;

using static theori.Platform.SDL.SDL;

namespace theori.IO
{
    public interface IGamepadListener
    {
        bool GamepadButtonPressed(ButtonInfo info);
        bool GamepadButtonReleased(ButtonInfo info);
        bool GamepadAxisChanged(AnalogInfo info);
    }

    public class ButtonInfo
    {
        public Gamepad Gamepad;

        public uint Button;

        public ButtonInfo(Gamepad gamepad, uint button)
        {
            Gamepad = gamepad;
            Button = button;
        }
    }

    public class AnalogInfo
    {
        public Gamepad Gamepad;

        public uint Axis;
        public float Value;

        public AnalogInfo(Gamepad gamepad, uint axis, float value)
        {
            Gamepad = gamepad;
            Axis = axis;
            Value = value;
        }
    }

    public class Gamepad
    {
        internal static Gamepad Create(int deviceIndex)
        {
            if (connected.ContainsKey(deviceIndex))
                throw new InvalidOperationException("Constructed a gamepad which shared a device index with already constructed gamepad");

            return connected[deviceIndex] = new Gamepad(deviceIndex);
        }

        public static void Remove(int instanceId)
        {
            if (TryGetFromInstanceId(instanceId) is { } gamepad)
            {
                gamepad.Close();
                connected.Remove(gamepad.DeviceIndex);
            }
        }

        public static Gamepad? TryGet(int deviceId) => connected.TryGetValue(deviceId, out var result) ? result : null;

        private static Gamepad? TryGetFromInstanceId(int instanceId)
        {
            foreach (var (deviceIndex, gamepad) in connected)
            {
                if (gamepad.InstanceId == instanceId)
                    return gamepad;
            }

            return null;
        }

        public static IEnumerable<Gamepad> Connected => connected.Values;

        private static readonly Dictionary<int, Gamepad> connected = new Dictionary<int, Gamepad>();

        internal static void HandleInputEvent(int instanceId, uint buttonIndex, uint newState)
        {
            if (TryGetFromInstanceId(instanceId) is { } gamepad)
                gamepad.HandleInputEvent(buttonIndex, newState);
        }

        internal static void HandleAxisEvent(int instanceId, uint axisIndex, short newValue)
        {
            if (TryGetFromInstanceId(instanceId) is { } gamepad)
                gamepad.HandleAxisEvent(axisIndex, newValue);
        }

        public readonly int DeviceIndex, InstanceId;

        public IntPtr Handle { get; private set; }

        public string? Name { get; private set; }

        public Guid Guid { get; private set; }
        public string? GuidString { get; private set; }

        public int NumButtons { get; private set; }
        public int NumAxes { get; private set; }
        public int NumHats { get; private set; }
        public int NumBalls { get; private set; }

        public event Action<ButtonInfo>? ButtonPressed;
        public event Action<ButtonInfo>? ButtonReleased;
        public event Action<AnalogInfo>? AxisChanged;

        private uint[]? m_buttonStates;
        private float[]? m_analogStates;

        private Gamepad(int deviceIndex)
        {
            DeviceIndex = deviceIndex;
            InstanceId = SDL_JoystickGetDeviceInstanceID(deviceIndex);
        }

        public bool Open()
        {
            Handle = SDL_JoystickOpen(DeviceIndex);
            if (Handle == IntPtr.Zero) return false;

            Name = SDL_JoystickName(Handle);
            Guid = SDL_JoystickGetGUID(Handle);

            NumButtons = SDL_JoystickNumButtons(Handle);
            NumAxes = SDL_JoystickNumAxes(Handle);
            NumHats = SDL_JoystickNumHats(Handle);
            NumBalls = SDL_JoystickNumBalls(Handle);

            const int STRING_BYTE_COUNT = 128;
            var guidStringBytes = new byte[STRING_BYTE_COUNT];
            SDL_JoystickGetGUIDString(Guid, guidStringBytes, STRING_BYTE_COUNT);

            GuidString = Encoding.ASCII.GetString(guidStringBytes, 0, guidStringBytes.IndexOf((byte)0));

            m_buttonStates = new uint[NumButtons];
            m_analogStates = new float[NumAxes];

            Logger.Log($"Opened Joystick: Device Index { DeviceIndex }, Instance ID { InstanceId }");
            Logger.Log($"  Name: { Name }");
            Logger.Log($"  Guid: { GuidString }");
            Logger.Log($"  Num Buttons: { NumButtons }");
            Logger.Log($"  Num Axes: { NumAxes }");
            Logger.Log($"  Num Hats: { NumHats }");
            Logger.Log($"  Num Balls: { NumBalls }");

            return true;
        }

        public void Close()
        {
            if (Handle == IntPtr.Zero) return;
            SDL_JoystickClose(Handle);
        }

        internal void HandleInputEvent(uint buttonIndex, uint newState)
        {
            var info = new ButtonInfo(this, buttonIndex);
            
            m_buttonStates![buttonIndex] = newState;
            if (newState == 1)
                ButtonPressed?.Invoke(info);
            else ButtonReleased?.Invoke(info);
        }

        internal void HandleAxisEvent(uint axisIndex, short newValue)
        {
            float value = newValue / (float)0x7FFF;
            var info = new AnalogInfo(this, axisIndex, value);

            m_analogStates![axisIndex] = value;
            AxisChanged?.Invoke(info);
        }
    }
}
