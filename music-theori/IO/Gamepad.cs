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
        public int DeviceIndex;

        public uint Button;
    }

    public class AnalogInfo
    {
        public int DeviceIndex;

        public uint Axis;
        public float Value;
    }

    public class NewGamepad
    {
        internal static NewGamepad Create(int deviceIndex)
        {
            if (connected.ContainsKey(deviceIndex))
                throw new InvalidOperationException("Constructed a gamepad which shared a device index with already constructed gamepad");

            return connected[deviceIndex] = new NewGamepad(deviceIndex);
        }

        public static void Remove(int instanceId)
        {
        }

        private static readonly Dictionary<int, NewGamepad> connected = new Dictionary<int, NewGamepad>();

        public readonly int DeviceIndex, InstanceId;

        public IntPtr Handle { get; private set; }

        public string? Name { get; private set; }

        public Guid Guid { get; private set; }
        public string? GuidString { get; private set; }

        public int NumButtons { get; private set; }
        public int NumAxes { get; private set; }
        public int NumHats { get; private set; }
        public int NumBalls { get; private set; }

        private NewGamepad(int deviceIndex)
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

            Logger.Log($"Opened Joystick: Device Index { DeviceIndex }, Instance ID { InstanceId }");
            Logger.Log($"  Name: { Name }");
            Logger.Log($"  Guid: { GuidString }");
            Logger.Log($"  Num Buttons: { NumButtons }");
            Logger.Log($"  Num Axes: { NumAxes }");
            Logger.Log($"  Num Hats: { NumHats }");
            Logger.Log($"  Num Balls: { NumBalls }");

            return true;
        }
    }

    public class Gamepad : Disposable
    {
        public static bool operator true (Gamepad g) => g.joystick != IntPtr.Zero;
        public static bool operator false(Gamepad g) => g.joystick == IntPtr.Zero;

        private static readonly Dictionary<int, Gamepad> connectedGamepads = new Dictionary<int, Gamepad>();
        private static readonly Dictionary<int, Gamepad> openGamepads = new Dictionary<int, Gamepad>();
        
        public static event Action<int>? Connect;
        public static event Action<int>? Disconnect;

        internal static void Destroy()
        {
            openGamepads.Clear();
        }

        public static int NumConnected => SDL_NumJoysticks();

        public static string NameOf(int deviceIndex)
        {
            return SDL_JoystickNameForIndex(deviceIndex);
        }

        public static Gamepad Open(int deviceIndex)
        {
            if (!openGamepads.TryGetValue(deviceIndex, out var gamepad))
            {
                SDL_JoystickEventState(SDL_ENABLE);
                gamepad = new Gamepad(deviceIndex);
                if (gamepad)
                    openGamepads[gamepad.InstanceId] = gamepad;
                else return null;
            }
            return gamepad;
        }

        internal static void HandleAddedEvent(int deviceIndex)
        {
            string name = SDL_JoystickNameForIndex(deviceIndex);
            Logger.Log($"Joystick Added: Device [{ deviceIndex }] \"{ name }\"", LogPriority.Verbose);

            Connect?.Invoke(deviceIndex);
        }

        internal static void HandleRemovedEvent(int instanceId)
        {
            Logger.Log($"Joystick Removed: Instance [{ instanceId }]", LogPriority.Verbose);
            
            Disconnect?.Invoke(instanceId);
            openGamepads.Remove(instanceId);
        }

        internal static void HandleInputEvent(int deviceIndex, uint buttonIndex, uint newState)
        {
            if (openGamepads.TryGetValue(deviceIndex, out var gamepad))
                gamepad.HandleInputEvent(buttonIndex, newState);
        }

        internal static void HandleAxisEvent(int deviceIndex, uint axisIndex, short newValue)
        {
            if (openGamepads.TryGetValue(deviceIndex, out var gamepad))
                gamepad.HandleAxisEvent(axisIndex, newValue);
        }
        
        public event Action<uint>? ButtonPressed;
        public event Action<uint>? ButtonReleased;
        public event Action<uint, float>? AxisChanged;

        public readonly int DeviceIndex;
        public readonly int InstanceId;

        private IntPtr joystick;
        
        private readonly uint[] buttonStates;
        private readonly float[] axisStates;

        public int ButtonCount => buttonStates.Length;
        public int AxisCount => axisStates.Length;

        public string DeviceName => SDL_JoystickName(joystick);

        private Gamepad(int deviceIndex)
        {
            joystick = SDL_JoystickOpen(deviceIndex);
            DeviceIndex = deviceIndex;

            if (joystick == IntPtr.Zero)
            {
                InstanceId = 0;
                Logger.Log($"Failed to open joystick { deviceIndex }.");
            }
            else
            {
                InstanceId = SDL_JoystickInstanceID(joystick);

                buttonStates = new uint[SDL_JoystickNumButtons(joystick)];
                axisStates = new float[SDL_JoystickNumAxes(joystick)];
                
                Logger.Log($"Opened joystick { deviceIndex } ({ DeviceName }) with { ButtonCount } buttons and { AxisCount } axes.");
            }
        }

        public void Close()
        {
            if (joystick != IntPtr.Zero) SDL_JoystickClose(joystick);
            joystick = IntPtr.Zero;

            openGamepads.Remove(InstanceId);
        }

        protected override void DisposeUnmanaged()
        {
            // TODO(local): throws access violation all the fkin time, but it's only at exit so it's like fine?
            //Close();
        }

        public bool ButtonDown(uint buttonIndex) => buttonStates[buttonIndex] != 0;
        public float GetAxis(uint axisIndex) => axisStates[axisIndex];

        internal void HandleInputEvent(uint buttonIndex, uint newState)
        {
            var info = new ButtonInfo()
            {
                DeviceIndex = DeviceIndex,
                Button = buttonIndex,
            };

            buttonStates[buttonIndex] = newState;
            if (newState == 1)
                ButtonPressed?.Invoke(buttonIndex);
            else ButtonReleased?.Invoke(buttonIndex);
        }

        internal void HandleAxisEvent(uint axisIndex, short newValue)
        {
            float value = newValue / (float)0x7FFF;
            axisStates[axisIndex] = value;
            AxisChanged?.Invoke(axisIndex, value);
            var info = new AnalogInfo()
            {
                DeviceIndex = DeviceIndex,
                Axis = axisIndex,
                Value = value,
            };
        }
    }
}
