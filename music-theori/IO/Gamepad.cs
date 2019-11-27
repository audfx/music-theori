using System;
using System.Collections.Generic;
using System.Text;
using MoonSharp.Interpreter;
using static theori.Platform.SDL.SDL;

namespace theori.IO
{
    public interface IGamepadListener
    {
        bool GamepadButtonPressed(GamepadButtonInfo info);
        bool GamepadButtonReleased(GamepadButtonInfo info);
        bool GamepadAxisChanged(GamepadAxisInfo info);
    }

    public struct GamepadButtonInfo
    {
        public Gamepad Gamepad;

        public uint Button;

        public GamepadButtonInfo(Gamepad gamepad, uint button)
        {
            Gamepad = gamepad;
            Button = button;
        }
    }

    public struct GamepadAxisInfo
    {
        public Gamepad Gamepad;

        public uint Axis;
        public float Value;

        public GamepadAxisInfo(Gamepad gamepad, uint axis, float value)
        {
            Gamepad = gamepad;
            Axis = axis;
            Value = value;
        }
    }

    public struct GamepadBallInfo
    {
        public Gamepad Gamepad;

        public uint Ball;
        public float XRelative, YRelative;

        public GamepadBallInfo(Gamepad gamepad, uint ball, float xRelative, float yRelative)
        {
            Gamepad = gamepad;
            Ball = ball;
            XRelative = xRelative;
            YRelative = xRelative;
        }
    }

    public class Gamepad : IEquatable<Gamepad>
    {
        public static bool operator ==(Gamepad a, Gamepad b) => a.Handle == b.Handle;
        public static bool operator !=(Gamepad a, Gamepad b) => a.Handle != b.Handle;

        private readonly HashSet<uint> lastButtons = new HashSet<uint>();
        private readonly HashSet<uint> heldButtons = new HashSet<uint>();

        public readonly int DeviceIndex, InstanceId;

        [MoonSharpHidden]
        public IntPtr Handle { get; private set; }

        public string Name { get; private set; } = "";

        [MoonSharpHidden]
        public Guid Guid { get; private set; }
        [MoonSharpHidden]
        public string? GuidString { get; private set; }

        public int NumButtons { get; private set; }
        public int NumAxes { get; private set; }
        public int NumHats { get; private set; }
        public int NumBalls { get; private set; }

        //private uint[]? m_buttonStates;
        //private float[]? m_analogStates;

        internal Gamepad(int deviceIndex)
        {
            DeviceIndex = deviceIndex;
            InstanceId = SDL_JoystickGetDeviceInstanceID(deviceIndex);
        }

        [MoonSharpHidden]
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

            //m_buttonStates = new uint[NumButtons];
            //m_analogStates = new float[NumAxes];

            Logger.Log($"Opened Joystick: Device Index { DeviceIndex }, Instance ID { InstanceId }");
            Logger.Log($"  Name: { Name }");
            Logger.Log($"  Guid: { GuidString }");
            Logger.Log($"  Num Buttons: { NumButtons }");
            Logger.Log($"  Num Axes: { NumAxes }");
            Logger.Log($"  Num Hats: { NumHats }");
            Logger.Log($"  Num Balls: { NumBalls }");

            return true;
        }

        [MoonSharpHidden]
        public void Close()
        {
            if (Handle == IntPtr.Zero) return;
            SDL_JoystickClose(Handle);
        }

        [MoonSharpHidden]
        public void Update()
        {
            lastButtons.Clear();
            foreach (uint button in heldButtons)
                lastButtons.Add(button);
        }

        internal IEnumerable<uint> GetAllHeldButtons() => heldButtons;

        internal bool Gamepad_ButtonPressed(uint button) => heldButtons.Add(button);
        internal bool Gamepad_ButtonReleased(uint button) => heldButtons.Remove(button);

        //internal void Gamepad_AxisChanged(uint axisIndex, float value) => m_analogStates![axisIndex] = value;

        public bool Equals(Gamepad other) => other == this;
        public override bool Equals(object? obj) => obj is Gamepad gp && gp == this;
        public override int GetHashCode() => HashCode.Combine(Handle, DeviceIndex, InstanceId);
    }
}
