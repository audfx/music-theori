//#define STORE_AXIS_DELTAS

using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace theori.IO
{
    public enum ControllerAxisStyle
    {
        Linear,
        Radial,
    }

    public struct ControllerButtonInfo
    {
        public Controller Controller;
        public HybridLabel Button;
    }

    public struct ControllerAxisInfo
    {
        public Controller Controller;
        public HybridLabel Axis;
        public float Value, Delta;
    }

    public struct ControllerAxisTickInfo
    {
        public Controller Controller;
        public HybridLabel Axis;
        public int Direction;
    }

    public class Controller
    {
        [MoonSharpHidden]
        public event Action<Controller, HybridLabel>? Pressed;
        [MoonSharpHidden]
        public event Action<Controller, HybridLabel>? Released;
        [MoonSharpHidden]
        public event Action<Controller, HybridLabel, float, float>? AxisChanged;
        [MoonSharpHidden]
        public event Action<Controller, HybridLabel, int>? AxisTicked;

        private readonly Dictionary<HybridLabel, bool> m_buttonStates = new Dictionary<HybridLabel, bool>();
        private readonly Dictionary<HybridLabel, float> m_axisValues = new Dictionary<HybridLabel, float>();
#if STORE_AXIS_DELTAS
        private readonly Dictionary<HybridLabel, float> m_axisDeltas = new Dictionary<HybridLabel, float>();
#endif

        private readonly Dictionary<HybridLabel, IControllerButton> m_buttons = new Dictionary<HybridLabel, IControllerButton>();
        private readonly Dictionary<HybridLabel, IControllerAxis> m_axes = new Dictionary<HybridLabel, IControllerAxis>();

        public string Name { get; }

        [MoonSharpHidden]
        public bool RequiresMouseGrabbed
        {
            get
            {
                foreach (var (label, button) in m_buttons)
                {
                    if (button is MouseControllerButton)
                        return true;
                }

                foreach (var (label, axis) in m_axes)
                {
                    if (axis is MouseMotionControllerAxis || axis is MouseButtonControllerAxis)
                        return true;
                }

                return false;
            }
        }

        [MoonSharpHidden]
        public Controller(string visibleName = ":theori Controller")
        {
            Name = visibleName;
        }

        [MoonSharpVisible(true)] private bool IsDown(int buttonId) => m_buttons[buttonId].IsDown;
        [MoonSharpVisible(true)] private bool IsDown(string buttonName) => m_buttons[buttonName].IsDown;

#if false
        public float GetAxisValue(HybridLabel label)
        {
            return 0.0f;
        }

#if STORE_AXIS_DELTAS
        public float GetAxisDelta(HybridLabel label)
        {
            return 0.0f;
        }
#endif
#endif

        [MoonSharpHidden]
        public void Update(float delta)
        {
            foreach (var (label, axis) in m_axes)
                axis.Update(delta);
        }

        internal IEnumerable<HybridLabel> GetAllHeldButtons() => from pair in m_buttonStates where pair.Value select pair.Key;

        private void ButtonPressedListener(HybridLabel buttonLabel) { Pressed?.Invoke(this, buttonLabel); m_buttonStates[buttonLabel] = true; }
        private void ButtonReleasedListener(HybridLabel buttonLabel) { Released?.Invoke(this, buttonLabel); m_buttonStates[buttonLabel] = false; }
        private void AxisChangedListener(HybridLabel axisLabel, float value, float delta) => AxisChanged?.Invoke(this, axisLabel, m_axisValues[axisLabel] = value,
#if STORE_AXIS_DELTAS
            m_axisDeltas[axisLabel] =
#endif
            delta);
        private void AxisTickListener(HybridLabel axisLabel, int tickDirection) => AxisTicked?.Invoke(this, axisLabel, tickDirection);

        private void RegisterButton(HybridLabel label, IControllerButton button)
        {
            if (m_buttons.TryGetValue(label, out var oldButton))
            {
                oldButton.Pressed -= ButtonPressedListener;
                oldButton.Released -= ButtonReleasedListener;
            }

            button.Label = label;
            m_buttons[label] = button;

            button.Pressed += ButtonPressedListener;
            button.Released += ButtonReleasedListener;
        }

        public void SetButtonToKey(HybridLabel label, KeyCode key) => RegisterButton(label, new KeyboardControllerButton(key));
        public void SetButtonToMouseButton(HybridLabel label, MouseButton button) => RegisterButton(label, new MouseControllerButton(button));
        public void SetButtonToGamepadButton(HybridLabel label, Gamepad gamepad, uint button) => RegisterButton(label, new GamepadControllerButton(gamepad, button));

        // TODO(local): possibly figure out how to convert (linear) axes to buttons

        private void RegisterAxis(HybridLabel label, IControllerAxis axis)
        {
            if (m_axes.TryGetValue(label, out var oldAxis))
            {
                oldAxis.Changed -= AxisChangedListener;
                oldAxis.Ticked -= AxisTickListener;
            }

            axis.Label = label;
            m_axes[label] = axis;

            axis.Changed += AxisChangedListener;
            axis.Ticked += AxisTickListener;
        }

        public void SetAxisToKeyLinear(HybridLabel label, KeyCode key0) => RegisterAxis(label, new KeyboardControllerAxis(ControllerAxisStyle.Linear, key0));
        public void SetAxisToKeysLinear(HybridLabel label, KeyCode key0, KeyCode key1) => RegisterAxis(label, new KeyboardControllerAxis(ControllerAxisStyle.Radial, key0, key1));
        public void SetAxisToKeyRadial(HybridLabel label, KeyCode key0) => RegisterAxis(label, new KeyboardControllerAxis(ControllerAxisStyle.Linear, key0));
        public void SetAxisToKeysRadial(HybridLabel label, KeyCode key0, KeyCode key1) => RegisterAxis(label, new KeyboardControllerAxis(ControllerAxisStyle.Radial, key0, key1));

        public void SetAxisToMouseButtonLinear(HybridLabel label, MouseButton button0) => RegisterAxis(label, new MouseButtonControllerAxis(ControllerAxisStyle.Linear, button0));
        public void SetAxisToMouseButtonsLinear(HybridLabel label, MouseButton button0, MouseButton button1) => RegisterAxis(label, new MouseButtonControllerAxis(ControllerAxisStyle.Radial, button0, button1));
        public void SetAxisToMouseButtonRadial(HybridLabel label, MouseButton button0) => RegisterAxis(label, new MouseButtonControllerAxis(ControllerAxisStyle.Linear, button0));
        public void SetAxisToMouseButtonsRadial(HybridLabel label, MouseButton button0, MouseButton button1) => RegisterAxis(label, new MouseButtonControllerAxis(ControllerAxisStyle.Radial, button0, button1));

        public void SetAxisToMouseAxis(HybridLabel label, Axis axis, float sens = 1.0f) => RegisterAxis(label, new MouseMotionControllerAxis(axis, sens));

        public void SetAxisToGamepadAxis(HybridLabel label, Gamepad gamepad, uint axis, ControllerAxisStyle style = ControllerAxisStyle.Linear, float sens = 1.0f, int smoothing = 5) => RegisterAxis(label, new GamepadAxisControllerAxis(style, gamepad, axis, sens, smoothing));

        internal bool Keyboard_KeyPress(KeyInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                if (button is KeyboardControllerButton kbutton && kbutton.Key == info.KeyCode)
                {
                    kbutton.OnPressed();
                    return true;
                }
            }

            foreach (var (label, axis) in m_axes)
            {
                if (axis is KeyboardControllerAxis kaxis)
                {
                    if (kaxis.Negative == info.KeyCode)
                    {
                        kaxis.PressNegative();
                        return true;
                    }
                    else if (kaxis.Positive == info.KeyCode)
                    {
                        kaxis.PressPositive();
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool Keyboard_KeyRelease(KeyInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                if (button is KeyboardControllerButton kbutton && kbutton.Key == info.KeyCode)
                {
                    kbutton.OnReleased();
                    return true;
                }
            }

            foreach (var (label, axis) in m_axes)
            {
                if (axis is KeyboardControllerAxis kaxis)
                {
                    if (kaxis.Negative == info.KeyCode)
                    {
                        kaxis.ReleaseNegative();
                        return true;
                    }
                    else if (kaxis.Positive == info.KeyCode)
                    {
                        kaxis.ReleasePositive();
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool Mouse_MouseButtonPress(MouseButtonInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                if (button is MouseControllerButton mbutton && mbutton.Button == info.Button)
                {
                    mbutton.OnPressed();
                    return true;
                }
            }

            foreach (var (label, axis) in m_axes)
            {
                if (axis is MouseButtonControllerAxis maxis)
                {
                    if (maxis.Negative == info.Button)
                    {
                        maxis.PressNegative();
                        return true;
                    }
                    else if (maxis.Positive == info.Button)
                    {
                        maxis.PressPositive();
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool Mouse_MouseButtonRelease(MouseButtonInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                if (button is MouseControllerButton mbutton && mbutton.Button == info.Button)
                {
                    mbutton.OnReleased();
                    return true;
                }
            }

            foreach (var (label, axis) in m_axes)
            {
                if (axis is MouseButtonControllerAxis maxis)
                {
                    if (maxis.Negative == info.Button)
                    {
                        maxis.ReleaseNegative();
                        return true;
                    }
                    else if (maxis.Positive == info.Button)
                    {
                        maxis.ReleasePositive();
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool Mouse_MouseMove(int x, int y, int dx, int dy)
        {
            bool anyAccepted = false;
            foreach (var (label, axis) in m_axes)
            {
                if (axis is MouseMotionControllerAxis maxis)
                {
                    maxis.Motion(dx, dy);
                    anyAccepted = true;
                }
            }

            return anyAccepted;
        }

        internal bool Gamepad_Pressed(GamepadButtonInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                if (button is GamepadControllerButton gbutton && gbutton.Button == info.Button)
                {
                    gbutton.OnPressed();
                    return true;
                }
            }

            return false;
        }

        internal bool Gamepad_Released(GamepadButtonInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                if (button is GamepadControllerButton gbutton && gbutton.Button == info.Button)
                {
                    gbutton.OnReleased();
                    return true;
                }
            }

            return false;
        }

        internal bool Gamepad_AxisMotion(GamepadAxisInfo info)
        {
            foreach (var (label, axis) in m_axes)
            {
                if (axis is GamepadAxisControllerAxis gaxis && gaxis.Axis == info.Axis)
                {
                    gaxis.Motion(info.Value);
                    return true;
                }
            }

            return false;
        }

        internal bool Gamepad_BallMotion(GamepadBallInfo info)
        {
            return false;
        }
    }

    internal interface IControllerButton
    {
        public HybridLabel Label { get; set; }

        public bool IsDown { get; }

        public event Action<HybridLabel>? Pressed;
        public event Action<HybridLabel>? Released;
    }

    internal interface IControllerAxis
    {
        public HybridLabel Label { get; set; }

        public float AxisValue { get; }
#if STORE_AXIS_DELTAS
        // delta is separate bc "radial"/"continuous" axes can loop around and need to report their deltas accordingly.
        public float AxisDelta { get; }
#endif
        public float PartialTick { get; }

        public event Action<HybridLabel, float, float>? Changed;
        public event Action<HybridLabel, int>? Ticked;

        void Update(float delta);
    }

    internal abstract class SimpleButtonTrigger : IControllerButton
    {
        public HybridLabel Label { get; set; }

        public bool IsDown { get; private set; }

        public event Action<HybridLabel>? Pressed;
        public event Action<HybridLabel>? Released;

        protected SimpleButtonTrigger()
        {
            Pressed += _ => IsDown = true;
            Released += _ => IsDown = false;
        }

        public void OnPressed() => Pressed?.Invoke(Label);
        public void OnReleased() => Released?.Invoke(Label);
    }

    internal sealed class KeyboardControllerButton : SimpleButtonTrigger
    {
        public readonly KeyCode Key;

        public KeyboardControllerButton(KeyCode key)
            : base()
        {
            Key = key;
        }
    }

    internal sealed class MouseControllerButton : SimpleButtonTrigger
    {
        public readonly MouseButton Button;

        public MouseControllerButton(MouseButton button)
            : base()
        {
            Button = button;
        }
    }

    internal sealed class GamepadControllerButton : SimpleButtonTrigger
    {
        public readonly Gamepad Gamepad;
        public readonly uint Button;

        public GamepadControllerButton(Gamepad gamepad, uint button)
            : base()
        {
            Gamepad = gamepad;
            Button = button;
        }
    }

    static class AxisHelper
    {
        private const float TickMotionMultiplier = 3;

        public static float AxisChanged(float partial, float axisDelta, Action<int> onTicked, float tickMotionMult = TickMotionMultiplier)
        {
            partial += axisDelta * tickMotionMult;

            while (partial <= -1)
            {
                onTicked(-1);
                partial += 1;
            }
            // `else while` would be great (triggers if initial condition was false before the first loop)
            while (partial >= 1)
            {
                onTicked(1);
                partial -= 1;
            }

            return partial;
        }

        public static float AxisDecay(float deltaTime, float partial)
        {
            if (partial < 0)
                partial = MathL.Min(partial + deltaTime * 2, 0);
            else partial = MathL.Max(partial - deltaTime * 2, 0);

            return partial;
        }
    }

    internal abstract class SimpleAxisButtonTrigger : IControllerAxis
    {
        public HybridLabel Label { get; set; }

        public enum RangeKind
        {
            Single, Double
        }

        public readonly ControllerAxisStyle Style;
        public readonly RangeKind Kind;

        public float AxisValue { get; private set; }
#if STORE_AXIS_DELTAS
        public float AxisDelta { get; private set; }
#endif

        public float PartialTick { get; private set; }

        public event Action<HybridLabel, float, float>? Changed;
        public event Action<HybridLabel, int>? Ticked;

        public float Speed = 2.0f;

        private bool m_positive, m_negative;
        private float m_currentValue, m_targetValue;

        /// <summary>
        /// Number of ticks per second.
        /// </summary>
        public float TickRate { get; set; } = 3;

        public SimpleAxisButtonTrigger(ControllerAxisStyle style, RangeKind kind)
        {
            Style = style;
            Kind = kind;
            Changed += (_, value, delta) =>
            {
                AxisValue = value;
#if STORE_AXIS_DELTAS
                AxisDelta = delta;
#endif
            };
        }

        public void Update(float delta)
        {
            if (m_negative != m_positive)
            {
                if (m_positive)
                {
                    if (PartialTick >= 1.0f)
                    {
                        OnTicked(1);
                        PartialTick -= 1.0f;
                    }
                    PartialTick += delta * TickRate;
                }
                else
                {
                    if (PartialTick <= -1.0f)
                    {
                        OnTicked(-1);
                        PartialTick += 1.0f;
                    }
                    PartialTick += delta * TickRate;
                }
            }
            else PartialTick =  PartialTick > 0 ? Math.Max(0, PartialTick - delta * 3 * TickRate)
                             : (PartialTick < 0 ? Math.Min(0, PartialTick + delta * 3 * TickRate)
                             :  PartialTick );

            if (Style == ControllerAxisStyle.Linear)
            {
                m_targetValue = (m_positive != m_negative) ? (m_positive ? 1 : -1) : 0;
                if (m_currentValue == m_targetValue) return;

                float previousValue = m_currentValue;
                if (m_targetValue < m_currentValue)
                    m_currentValue = MathL.Min(m_targetValue, m_currentValue + delta * Speed);
                else m_currentValue = MathL.Max(m_targetValue, m_currentValue - delta * Speed);

                OnChanged(m_currentValue, m_currentValue - previousValue);
            }
            else
            {
                float axisDelta = delta * Speed * ((m_positive != m_negative) ? (m_positive ? 1 : -1) : 0);
                m_currentValue += axisDelta;

                if (m_currentValue > 1)
                    m_currentValue -= 2;
                else if (m_currentValue < -1)
                    m_currentValue += 2;

                OnChanged(m_currentValue, axisDelta);
            }
        }

        internal void PressPositive() => m_positive = true;
        internal void ReleasePositive() => m_positive = false;

        internal void PressNegative() => m_negative = true;
        internal void ReleaseNegative() => m_negative = false;

        private void OnChanged(float value, float delta) => Changed?.Invoke(Label, value, delta);
        private void OnTicked(int direction) => Ticked?.Invoke(Label, direction);
    }

    internal sealed class KeyboardControllerAxis : SimpleAxisButtonTrigger
    {
        public readonly KeyCode Positive, Negative;

        public KeyboardControllerAxis(ControllerAxisStyle style, KeyCode positive, KeyCode negative = KeyCode.UNKNOWN)
            : base(style, negative == KeyCode.UNKNOWN ? RangeKind.Single : RangeKind.Double)
        {
            Positive = positive;
            Negative = negative;
        }
    }

    internal sealed class MouseButtonControllerAxis : SimpleAxisButtonTrigger
    {
        public readonly MouseButton Positive, Negative;

        public MouseButtonControllerAxis(ControllerAxisStyle style, MouseButton positive, MouseButton negative = MouseButton.Unknown)
            : base(style, negative == MouseButton.Unknown ? RangeKind.Single : RangeKind.Double)
        {
            Positive = positive;
            Negative = negative;
        }
    }

    internal sealed class MouseMotionControllerAxis : IControllerAxis
    {
        public HybridLabel Label { get; set; }

        public readonly Axis Axis;
        public readonly float Sensitivity;

        public float AxisValue { get; set; }
#if STORE_AXIS_DELTAS
        public float AxisDelta { get; set; }
#endif

        public event Action<HybridLabel, float, float>? Changed;
        public event Action<HybridLabel, int>? Ticked;

        public float PartialTick { get; private set; }

        public MouseMotionControllerAxis(Axis axis, float sens = 1.0f)
        {
            Axis = axis;
            Sensitivity = sens;
            Changed += (_, value, delta) =>
            {
                AxisValue = value;
#if STORE_AXIS_DELTAS
                AxisDelta = delta;
#endif
            };
        }

        public void Update(float delta)
        {
#if STORE_AXIS_DELTAS
            AxisDelta = 0.0f;
#endif

            PartialTick = AxisHelper.AxisDecay(delta, PartialTick);
        }

        public void Motion(int dx, int dy)
        {
            switch (Axis)
            {
                case Axis.X: OnChanged(AxisValue + dx * Sensitivity, dx * Sensitivity); break;
                case Axis.Y: OnChanged(AxisValue + dy * Sensitivity, dy * Sensitivity); break;
            }
        }

        private void OnChanged(float value, float delta)
        {
            PartialTick = AxisHelper.AxisChanged(PartialTick, delta, OnTicked);
            Changed?.Invoke(Label, value, delta);
        }

        private void OnTicked(int direction) => Ticked?.Invoke(Label, direction);
    }

    internal sealed class GamepadAxisControllerAxis : IControllerAxis
    {
        public HybridLabel Label { get; set; }

        public readonly Gamepad Gamepad;
        public readonly ControllerAxisStyle Style;
        public readonly uint Axis;
        public readonly float Sensitivity;
        public readonly int SampleSmoothing;

        public float AxisValue { get; set; }
#if STORE_AXIS_DELTAS
        public float AxisDelta => m_currentDelta;
#endif

        public event Action<HybridLabel, float, float>? Changed;
        public event Action<HybridLabel, int>? Ticked;

        public float PartialTick { get; private set; }

        private float m_axisPrevious, m_axisAverageDelta;
#if STORE_AXIS_DELTAS
        private float m_currentDelta, m_nextDelta;
#endif

        private readonly float[] m_axisDeltas;

        /// <summary>
        /// Number of ticks per second for linear axes.
        /// </summary>
        public float TickRate { get; set; } = 3;

        public GamepadAxisControllerAxis(ControllerAxisStyle style, Gamepad gamepad, uint axis, float sens = 1.0f, int smoothing = 5)
        {
            Gamepad = gamepad;
            Style = style;
            Axis = axis;
            Sensitivity = sens;
            SampleSmoothing = smoothing;
            Changed += (_, value, delta) =>
            {
                AxisValue = value;
#if STORE_AXIS_DELTAS
                AxisDelta = delta;
#endif
            };

            m_axisDeltas = new float[smoothing];
        }

        public void Update(float delta)
        {
#if STORE_AXIS_DELTAS
            m_currentDelta = m_nextDelta;
            m_nextDelta = 0;
#endif

            if (Style == ControllerAxisStyle.Radial)
            {
                PartialTick = AxisHelper.AxisDecay(delta, PartialTick);
            }
            else
            {
                float tickRate = TickRate * m_axisPrevious; // normalized, scales the tick rate

                if (m_axisPrevious > 0)
                {
                    if (PartialTick >= 1.0f)
                    {
                        OnTicked(1);
                        PartialTick -= 1.0f;
                    }
                    PartialTick += delta * tickRate;
                }
                else if (m_axisPrevious < 0)
                {
                    if (PartialTick <= -1.0f)
                    {
                        OnTicked(-1);
                        PartialTick += 1.0f;
                    }
                    PartialTick += delta * tickRate;
                }
                else PartialTick = PartialTick > 0 ? Math.Max(0, PartialTick - delta * 3 * tickRate)
                                 : (PartialTick < 0 ? Math.Min(0, PartialTick + delta * 3 * tickRate)
                                 : PartialTick);
            }
        }

        public void Motion(float value)
        {
            float p = m_axisPrevious;
            float c = value;

            if (Style == ControllerAxisStyle.Radial)
            {
                float delta = c - p;
                if (p > 0.9f && c < -0.9f)
                    delta = m_axisAverageDelta;
                else if (p < -0.9f && c > 0.9f)
                    delta = -m_axisAverageDelta;
                else if (delta > 0)
                    m_axisAverageDelta = (m_axisAverageDelta + delta) * 0.5f;

                m_axisPrevious = value;
#if STORE_AXIS_DELTAS
                m_nextDelta = delta;
#endif

                for (int i = m_axisDeltas.Length - 1; i >= 1; i--)
                    m_axisDeltas[i] = m_axisDeltas[i - 1];
                m_axisDeltas[0] = delta;

                OnChanged(value, delta);
            }
            else
            {
                OnChanged(value, value - m_axisPrevious);
                m_axisPrevious = value;
            }
        }

        private void OnChanged(float value, float delta)
        {
            if (Style == ControllerAxisStyle.Radial)
            {
                PartialTick = AxisHelper.AxisChanged(PartialTick, delta, OnTicked);
            }

            Changed?.Invoke(Label, value, delta);
        }

        private void OnTicked(int direction) => Ticked?.Invoke(Label, direction);
    }
}
