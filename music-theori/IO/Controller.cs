//#define STORE_AXIS_DELTAS

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    internal class ControllerButton
    {
        public HybridLabel ButtonId { get; }

        public readonly Dictionary<object, IControllerButtonBinding> Bindings = new Dictionary<object, IControllerButtonBinding>();

        private readonly HashSet<IControllerButtonBinding> m_pressed = new HashSet<IControllerButtonBinding>();

        public bool IsDown => Bindings.Values.Any(binding => binding.IsDown);

        public event Action<HybridLabel>? Pressed;
        public event Action<HybridLabel>? Released;

        public ControllerButton(HybridLabel buttonId)
        {
            ButtonId = buttonId;
        }

        public void RegisterBinding(object key, IControllerButtonBinding binding)
        {
            if (Bindings.TryGetValue(key, out var lastBinding))
            {
                lastBinding.Pressed -= BindingPressed;
                lastBinding.Released -= BindingReleased;
            }

            binding.Label = ButtonId;
            Bindings[key] = binding;

            binding.Pressed += BindingPressed;
            binding.Released += BindingReleased;
        }

        public void RemoveAllBindings()
        {
            foreach (var (key, binding) in Bindings)
            {
                binding.Pressed -= BindingPressed;
                binding.Released -= BindingReleased;
            }

            Bindings.Clear();
        }

        private void BindingPressed(IControllerButtonBinding binding, HybridLabel obj)
        {
            m_pressed.Add(binding);
            if (m_pressed.Count == 1)
                Pressed?.Invoke(ButtonId);
        }

        private void BindingReleased(IControllerButtonBinding binding, HybridLabel obj)
        {
            m_pressed.Remove(binding);
            if (m_pressed.Count == 0)
                Released?.Invoke(ButtonId);
        }
    }

    internal class ControllerAxis
    {
        public HybridLabel AxisId { get; }

        public readonly Dictionary<(object Positive, object? Negative), IControllerAxisBinding> Bindings = new Dictionary<(object, object?), IControllerAxisBinding>();

        public event Action<HybridLabel, float, float>? AxisChanged;
        public event Action<HybridLabel, int>? AxisTicked;

        public ControllerAxis(HybridLabel buttonId)
        {
            AxisId = buttonId;
        }

        public void RegisterBinding(object key0, object? key1, IControllerAxisBinding binding)
        {
            var key = (key0, key1);

            if (Bindings.TryGetValue(key, out var lastBinding))
            {
                lastBinding.Changed -= BindingChanged;
                lastBinding.Ticked -= BindingTicked;
            }

            binding.Label = AxisId;
            Bindings[key] = binding;

            binding.Changed += BindingChanged;
            binding.Ticked += BindingTicked;
        }

        public void RemoveAllBindings()
        {
            foreach (var (key, binding) in Bindings)
            {
                binding.Changed -= BindingChanged;
                binding.Ticked -= BindingTicked;
            }

            Bindings.Clear();
        }

        private void BindingChanged(IControllerAxisBinding binding, HybridLabel obj, float value, float delta)
        {
            AxisChanged?.Invoke(AxisId, value, delta);
        }

        private void BindingTicked(IControllerAxisBinding binding, HybridLabel obj, int direction)
        {
            AxisTicked?.Invoke(AxisId, direction);
        }
    }

    public class Controller
    {
        struct GamepadButtonPair
        {
            public string Name;
            public uint Button;

            public GamepadButtonPair(string name, uint button)
            {
                Name = name;
                Button = button;
            }
        }

        struct GamepadAxisPair
        {
            public string Name;
            public uint Axis;

            public GamepadAxisPair(string name, uint axis)
            {
                Name = name;
                Axis = axis;
            }
        }

        public static Controller? TryCreateFromFile(string filePath)
        {
            using var reader = new JsonTextReader(new StreamReader(File.OpenRead(filePath)));
            var jObject = JObject.Load(reader);

            var obj = (dynamic)jObject;

            //try
            //{
                string name = obj.name;
                var con = new Controller(name);

                foreach (var button in obj.buttons)
                {
                    HybridLabel id = button.id.Type == JTokenType.Integer ? (HybridLabel)(int)button.id : (HybridLabel)(string)button.id;
                    foreach (var binding in button.bindings)
                    {
                        string key = binding.key;
                        if (key.TrySplit(':', out string kind, out string value))
                        {
                            var result = ParseKey(kind, value);
                            if (result is KeyCode keyCode)
                                con.SetButtonToKey(id, keyCode);
                            else if (result is MouseButton mouseButton)
                                con.SetButtonToMouseButton(id, mouseButton);
                            else if (result is (Gamepad gamepad, uint gamepadButton))
                                con.SetButtonToGamepadButton(id, gamepad, gamepadButton);
                        }
                    }
                }

                foreach (var axis in obj.axes)
                {
                    HybridLabel id = axis.id.Type == JTokenType.Integer ? (HybridLabel)(int)axis.id : (HybridLabel)(string)axis.id;
                    foreach (var binding in axis.bindings)
                    {
                        string keyPositive = binding.keyPositive;
                        if (((JObject)binding).ContainsKey("keyNegative"))
                        {
                            string keyNegative = binding.keyNegative;
                            if (keyPositive.TrySplit(':', out string kind, out string value))
                            {
                                if (!keyNegative.TrySplit(':', out string kind2, out string value2) || kind2 != kind) continue;

                                var resultPos = ParseKey(kind, value);
                                var resultNeg = ParseKey(kind, value2);

                                if (resultPos is KeyCode keyCode && resultNeg is KeyCode keyCode2)
                                {
                                    if (((JObject)binding).ContainsKey("style") && binding.style == ControllerAxisStyle.Linear)
                                        con.SetAxisToKeysLinear(id, keyCode, keyCode2);
                                    else con.SetAxisToKeysRadial(id, keyCode, keyCode2);
                                }
                                else if (resultPos is MouseButton mouseButton && resultNeg is MouseButton mouseButton2)
                                {
                                    if (((JObject)binding).ContainsKey("style") && binding.style == ControllerAxisStyle.Linear)
                                        con.SetAxisToMouseButtonsLinear(id, mouseButton, mouseButton2);
                                    else con.SetAxisToMouseButtonsRadial(id, mouseButton, mouseButton2);
                                }
                            }
                        }
                        else
                        {
                            if (keyPositive.TrySplit(':', out string kind, out string value))
                            {
                                var result = ParseKey(kind, value);

                                if (result is KeyCode keyCode)
                                {
                                    if (((JObject)binding).ContainsKey("style") && binding.style == ControllerAxisStyle.Linear)
                                        con.SetAxisToKeyLinear(id, keyCode);
                                    else con.SetAxisToKeyRadial(id, keyCode);
                                }
                                else if (result is MouseButton mouseButton)
                                {
                                    if (((JObject)binding).ContainsKey("style") && binding.style == ControllerAxisStyle.Linear)
                                        con.SetAxisToMouseButtonLinear(id, mouseButton);
                                    else con.SetAxisToMouseButtonRadial(id, mouseButton);
                                }
                                else if (result is (Gamepad gamepad, uint gamepadIndex))
                                {
                                    var style = ((JObject)binding).ContainsKey("style") ? (ControllerAxisStyle)binding.style : ControllerAxisStyle.Linear;
                                    con.SetAxisToGamepadAxis(id, gamepad, gamepadIndex, style, (float)binding.sens, (int)binding.smoothing);
                                }
                                else if (result is Axis mouseAxis)
                                {
                                    con.SetAxisToMouseAxis(id, mouseAxis, (float)binding.sens);
                                }
                            }
                        }
                    }
                }

                static object? ParseKey(string kind, string value)
                {
                    switch (kind)
                    {
                        case "Key": return Enum.Parse<KeyCode>(value);
                        case "MouseButton": return Enum.Parse<MouseButton>(value);
                        case "MouseAxis": return Enum.Parse<Axis>(value);
                        case "GamepadButton":
                        {
                            if (value.TrySplit(',', out string gamepadName, out string buttonIndex) && UserInputService.TryGetGamepadFromName(gamepadName) is Gamepad gamepad)
                                return (gamepad, uint.Parse(buttonIndex));
                        } break;
                        case "GamepadAxis":
                        {
                            if (value.TrySplit(',', out string gamepadName, out string axisIndex) && UserInputService.TryGetGamepadFromName(gamepadName) is Gamepad gamepad)
                                return (gamepad, uint.Parse(axisIndex));
                        } break;
                    }
                    return null;
                }

                return con;
            //}
            //catch (Exception e) { }

            return null;
        }

        [MoonSharpHidden]
        public void SaveToFile(string? filePath = null)
        {
            filePath ??= $"controllers/{Name}.json";

            Directory.CreateDirectory(Directory.GetParent(filePath).FullName);
            using var writer = new JsonTextWriter(new StreamWriter(File.Open(filePath, FileMode.Create)));

            writer.WriteStartObject();
            {
                writer.WritePropertyName("name");
                writer.WriteValue(Name);

                writer.WritePropertyName("buttons");
                writer.WriteStartArray();
                {
                    foreach (var (id, button) in m_buttons)
                    {
                        writer.WriteStartObject();
                        {
                            writer.WritePropertyName("id");
                            if (id.LabelKind == HybridLabel.Kind.Number)
                                writer.WriteValue((int)id);
                            else writer.WriteValue((string)id);

                            writer.WritePropertyName("bindings");
                            writer.WriteStartArray();
                            {
                                foreach (var (key, binding) in button.Bindings)
                                {
                                    writer.WriteStartObject();
                                    writer.WritePropertyName("key");
                                    WriteKeyValue(key);
                                    writer.WriteEndObject();
                                }
                            }
                            writer.WriteEndArray();
                        }
                        writer.WriteEndObject();
                    }
                }
                writer.WriteEndArray();

                writer.WritePropertyName("axes");
                writer.WriteStartArray();
                {
                    foreach (var (id, axis) in m_axes)
                    {
                        writer.WriteStartObject();
                        {
                            writer.WritePropertyName("id");
                            if (id.LabelKind == HybridLabel.Kind.Number)
                                writer.WriteValue((int)id);
                            else writer.WriteValue((string)id);

                            writer.WritePropertyName("bindings");
                            writer.WriteStartArray();
                            {
                                foreach (var (key, binding) in axis.Bindings)
                                {
                                    writer.WriteStartObject();
                                    writer.WritePropertyName("keyPositive");
                                    WriteKeyValue(key.Positive);

                                    if (key.Negative != null)
                                    {
                                        writer.WritePropertyName("keyNegative");
                                        WriteKeyValue(key.Negative!);
                                    }

                                    binding.WriteDataToJson(writer);
                                    writer.WriteEndObject();
                                }
                            }
                            writer.WriteEndArray();
                        }
                        writer.WriteEndObject();
                    }
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();

            void WriteKeyValue(object key)
            {
                if (key is KeyCode keyCode)
                    writer.WriteValue($"Key:{keyCode}");
                else if (key is MouseButton mouseButton)
                    writer.WriteValue($"MouseButton:{mouseButton}");
                else if (key is Axis mouseAxis)
                    writer.WriteValue($"MouseAxis:{mouseAxis}");
                else if (key is GamepadButtonPair buttonPair)
                    writer.WriteValue($"GamepadButton:{buttonPair.Name},{buttonPair.Button}");
                else if (key is GamepadAxisPair axisPair)
                    writer.WriteValue($"GamepadAxis:{axisPair.Name},{axisPair.Axis}");
            }
        }

        [MoonSharpVisible(true)]
        private void Save() => SaveToFile();

        [MoonSharpHidden]
        public event Action<Controller, HybridLabel>? Pressed;
        [MoonSharpHidden]
        public event Action<Controller, HybridLabel>? Released;
        [MoonSharpHidden]
        public event Action<Controller, HybridLabel, float, float>? AxisChanged;
        [MoonSharpHidden]
        public event Action<Controller, HybridLabel, int>? AxisTicked;

        private readonly Dictionary<HybridLabel, ControllerButton> m_buttons = new Dictionary<HybridLabel, ControllerButton>();
        private readonly Dictionary<HybridLabel, ControllerAxis> m_axes = new Dictionary<HybridLabel, ControllerAxis>();

        public string Name { get; }

        [MoonSharpHidden]
        public bool RequiresMouseGrabbed
        {
            get
            {
                foreach (var (label, button) in m_buttons)
                {
                    foreach (var (key, binding) in button.Bindings)
                    {
                        if (binding is MouseControllerButton)
                            return true;
                    }
                }

                foreach (var (label, axis) in m_axes)
                {
                    foreach (var (key, binding) in axis.Bindings)
                    {
                        if (binding is MouseMotionControllerAxis || binding is MouseButtonControllerAxis)
                            return true;
                    }
                }

                return false;
            }
        }

        [MoonSharpHidden]
        public Controller(string visibleName = ":theori Controller")
        {
            Name = visibleName;
        }

        [MoonSharpVisible(true)] private bool IsDown(int buttonId) => m_buttons.Where(button => button.Key == buttonId).Any(button => button.Value.Bindings.Any(binding => binding.Value.IsDown)); //m_buttons[buttonId].IsDown;
        [MoonSharpVisible(true)] private bool IsDown(string buttonName) => m_buttons.Where(button => button.Key == buttonName).Any(button => button.Value.Bindings.Any(binding => binding.Value.IsDown)); //m_buttons[buttonName].IsDown;

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
            {
                foreach (var (key, binding) in axis.Bindings)
                    binding.Update(delta);
            }
        }

        public void RemoveAllButtonBindings(HybridLabel bindingLabel)
        {
            if (m_buttons.TryGetValue(bindingLabel, out var button))
                button.RemoveAllBindings();
            m_buttons.Remove(bindingLabel);
        }

        public void RemoveAllAxisBindings(HybridLabel bindingLabel)
        {
            if (m_axes.TryGetValue(bindingLabel, out var axis))
                axis.RemoveAllBindings();
            m_axes.Remove(bindingLabel);
        }

        [MoonSharpVisible(true)]
        private List<Dictionary<string, object>> GetButtonBindings(HybridLabel buttonLabel)
        {
            if (!m_buttons.TryGetValue(buttonLabel, out var button))
                return new List<Dictionary<string, object>>();

            var result = new List<Dictionary<string, object>>();
            foreach (var binding in button.Bindings)
            {
                object device, input;
                if (binding.Value is KeyboardControllerButton kb)
                {
                    device = "keyboard";
                    input = kb.Key;
                }
                else if (binding.Value is MouseControllerButton mb)
                {
                    device = "mouse";
                    input = mb.Button;
                }
                else if (binding.Value is GamepadControllerButton gb)
                {
                    device = gb.Gamepad;
                    input = gb.Button;
                }
                else continue;

                result.Add(new Dictionary<string, object>()
                {
                    { "device", device },
                    { "input", input },
                });
            }

            return result;
        }

        [MoonSharpVisible(true)]
        private List<Dictionary<string, object>> GetAxisBindings(HybridLabel axisLabel)
        {
            if (!m_axes.TryGetValue(axisLabel, out var axis))
                return new List<Dictionary<string, object>>();

            var result = new List<Dictionary<string, object>>();
            foreach (var binding in axis.Bindings)
            {
                var b = new Dictionary<string, object>();
                if (binding.Value is KeyboardControllerAxis kb)
                {
                    b["device"] = "keyboard";
                    b["input"] = kb.Positive;
                    b["input2"] = kb.Negative;
                }
                else if (binding.Value is MouseButtonControllerAxis mb)
                {
                    b["device"] = "mouse";
                    b["input"] = mb.Positive;
                    b["input2"] = mb.Negative;
                }
                else if (binding.Value is MouseMotionControllerAxis mm)
                {
                    b["device"] = "mouse";
                    b["input"] = "motion";
                    b["axis"] = mm.Axis;
                }
                else if (binding.Value is GamepadAxisControllerAxis ga)
                {
                    b["device"] = ga.Gamepad;
                    b["input"] = ga.Axis;
                    b["axis"] = ga.Axis;
                }
                else continue;

                result.Add(b);
            }

            return result;
        }

        [MoonSharpVisible(true)]
        private void SetButtonBindings(HybridLabel buttonLabel, List<Dictionary<string, object>> bindings)
        {
            RemoveAllButtonBindings(buttonLabel);
            foreach (var binding in bindings)
            {
                object? device = binding.TryGetValue("device", out var d) ? d : null;
                object? input = binding.TryGetValue("input", out var i) ? i : null;
                object? axis = binding.TryGetValue("axis", out var a) ? a : null;

                if (device is string deviceName)
                {
                    if (deviceName == "keyboard" && input is KeyCode keyCode)
                        SetButtonToKey(buttonLabel, keyCode);
                    else if (deviceName == "mouse" && input is MouseButton mouseButton)
                        SetButtonToMouseButton(buttonLabel, mouseButton);
                }
                else if (device is Gamepad gamepad)
                {
                    try
                    {
                        uint button = (uint)Convert.ChangeType(input, typeof(uint));
                        SetButtonToGamepadButton(buttonLabel, gamepad, button);
                    }
                    catch { }
                }
            }
        }

        [MoonSharpVisible(true)]
        private void SetAxisBindings(HybridLabel axisLabel, List<Dictionary<string, object>> bindings)
        {
            RemoveAllAxisBindings(axisLabel);
            foreach (var binding in bindings)
            {
                object? device = binding.TryGetValue("device", out var d) ? d : null;
                object? input = binding.TryGetValue("input", out var i) ? i : null;
                object? input2 = binding.TryGetValue("input2", out i) ? i : null;
                object? axis = binding.TryGetValue("axis", out var a) ? a : null;

                var axisStyle = binding.TryGetValue("axisStyle", out var s) && s is ControllerAxisStyle style ? style : ControllerAxisStyle.Linear;
                float sens = (float)(binding.TryGetValue("sensitivity", out var x) && x is double xs ? xs : 1.0);

                if (device is string deviceName)
                {
                    if (deviceName == "keyboard" && input is KeyCode keyCode)
                    {
                        if (input2 is KeyCode keyCode2)
                        {
                            if (axisStyle == ControllerAxisStyle.Linear)
                                SetAxisToKeysLinear(axisLabel, keyCode, keyCode2);
                            else SetAxisToKeysRadial(axisLabel, keyCode, keyCode2);
                        }
                        else
                        {
                            if (axisStyle == ControllerAxisStyle.Linear)
                                SetAxisToKeyLinear(axisLabel, keyCode);
                            else SetAxisToKeyRadial(axisLabel, keyCode);
                        }
                    }
                    else if (deviceName == "mouse")
                    {
                        if (input is string inputName)
                        {
                            if (inputName == "motion" && axis is Axis axisAxis)
                                SetAxisToMouseAxis(axisLabel, axisAxis, sens);
                            //else if (inputName == "wheel")
                        }
                        else if (input is MouseButton mouseButton)
                        {
                            if (input2 is MouseButton mouseButton2)
                            {
                                if (axisStyle == ControllerAxisStyle.Linear)
                                    SetAxisToMouseButtonsLinear(axisLabel, mouseButton, mouseButton2);
                                else SetAxisToMouseButtonsRadial(axisLabel, mouseButton, mouseButton2);
                            }
                            else
                            {
                                if (axisStyle == ControllerAxisStyle.Linear)
                                    SetAxisToMouseButtonLinear(axisLabel, mouseButton);
                                else SetAxisToMouseButtonRadial(axisLabel, mouseButton);
                            }
                        }
                    }
                }
                else if (device is Gamepad gamepad)
                {
                    try
                    {
                        uint axisValue = (uint)Convert.ChangeType(input, typeof(uint));
                        SetAxisToGamepadAxis(axisLabel, gamepad, axisValue, axisStyle, sens);
                    }
                    catch { }
                }
            }
        }

        internal IEnumerable<HybridLabel> GetAllHeldButtons() => from pair in m_buttons where pair.Value.IsDown select pair.Key;

        private void ButtonPressedListener(HybridLabel buttonLabel) { Pressed?.Invoke(this, buttonLabel); }
        private void ButtonReleasedListener(HybridLabel buttonLabel) { Released?.Invoke(this, buttonLabel); }
        private void AxisChangedListener(HybridLabel axisLabel, float value, float delta) => AxisChanged?.Invoke(this, axisLabel, value, delta);
        private void AxisTickListener(HybridLabel axisLabel, int tickDirection) => AxisTicked?.Invoke(this, axisLabel, tickDirection);

        private void RegisterButton(HybridLabel label, object key, IControllerButtonBinding binding)
        {
            if (!m_buttons.TryGetValue(label, out var button))
            {
                button = m_buttons[label] = new ControllerButton(label);

                button.Pressed += ButtonPressedListener;
                button.Released += ButtonReleasedListener;
            }

            binding.Label = label;
            button.RegisterBinding(key, binding);
        }

        public void SetButtonToKey(HybridLabel label, KeyCode key) => RegisterButton(label, key, new KeyboardControllerButton(key));
        public void SetButtonToMouseButton(HybridLabel label, MouseButton button) => RegisterButton(label, button, new MouseControllerButton(button));
        public void SetButtonToGamepadButton(HybridLabel label, Gamepad gamepad, uint button) => RegisterButton(label, new GamepadButtonPair(gamepad.Name, button), new GamepadControllerButton(gamepad, button));

        // TODO(local): possibly figure out how to convert (linear) axes to buttons

        private void RegisterAxis(HybridLabel label, object key0, object? key1, IControllerAxisBinding binding)
        {
            if (!m_axes.TryGetValue(label, out var axis))
            {
                axis = m_axes[label] = new ControllerAxis(label);

                axis.AxisChanged += AxisChangedListener;
                axis.AxisTicked += AxisTickListener;
            }

            binding.Label = label;
            axis.RegisterBinding(key0, key1, binding);
        }

        public void SetAxisToKeyLinear(HybridLabel label, KeyCode key0) => RegisterAxis(label, key0, null, new KeyboardControllerAxis(ControllerAxisStyle.Linear, key0));
        public void SetAxisToKeysLinear(HybridLabel label, KeyCode key0, KeyCode key1) => RegisterAxis(label, key0, key1, new KeyboardControllerAxis(ControllerAxisStyle.Linear, key0, key1));
        public void SetAxisToKeyRadial(HybridLabel label, KeyCode key0) => RegisterAxis(label, key0, null, new KeyboardControllerAxis(ControllerAxisStyle.Radial, key0));
        public void SetAxisToKeysRadial(HybridLabel label, KeyCode key0, KeyCode key1) => RegisterAxis(label, key0, key1, new KeyboardControllerAxis(ControllerAxisStyle.Radial, key0, key1));

        public void SetAxisToMouseButtonLinear(HybridLabel label, MouseButton button0) => RegisterAxis(label, button0, null, new MouseButtonControllerAxis(ControllerAxisStyle.Linear, button0));
        public void SetAxisToMouseButtonsLinear(HybridLabel label, MouseButton button0, MouseButton button1) => RegisterAxis(label, button0, button1, new MouseButtonControllerAxis(ControllerAxisStyle.Linear, button0, button1));
        public void SetAxisToMouseButtonRadial(HybridLabel label, MouseButton button0) => RegisterAxis(label, button0, null, new MouseButtonControllerAxis(ControllerAxisStyle.Radial, button0));
        public void SetAxisToMouseButtonsRadial(HybridLabel label, MouseButton button0, MouseButton button1) => RegisterAxis(label, button0, button1, new MouseButtonControllerAxis(ControllerAxisStyle.Radial, button0, button1));

        public void SetAxisToMouseAxis(HybridLabel label, Axis axis, float sens = 1.0f) => RegisterAxis(label, axis, null, new MouseMotionControllerAxis(axis, sens));

        public void SetAxisToGamepadAxis(HybridLabel label, Gamepad gamepad, uint axis, ControllerAxisStyle style = ControllerAxisStyle.Linear, float sens = 1.0f, int smoothing = 5) =>
            RegisterAxis(label, new GamepadAxisPair(gamepad.Name, axis), null, new GamepadAxisControllerAxis(style, gamepad, axis, sens, smoothing));

        internal bool Keyboard_KeyPress(KeyInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                foreach (var (key, binding) in button.Bindings)
                {
                    if (binding is KeyboardControllerButton kbutton && kbutton.Key == info.KeyCode)
                    {
                        kbutton.OnPressed();
                        return true;
                    }
                }
            }

            foreach (var (label, axis) in m_axes)
            {
                foreach (var (key, binding) in axis.Bindings)
                {
                    if (binding is KeyboardControllerAxis kaxis)
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
            }

            return false;
        }

        internal bool Keyboard_KeyRelease(KeyInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                foreach (var (key, binding) in button.Bindings)
                {
                    if (binding is KeyboardControllerButton kbutton && kbutton.Key == info.KeyCode)
                    {
                        kbutton.OnReleased();
                        return true;
                    }
                }
            }

            foreach (var (label, axis) in m_axes)
            {
                foreach (var (key, binding) in axis.Bindings)
                {
                    if (binding is KeyboardControllerAxis kaxis)
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
            }

            return false;
        }

        internal bool Mouse_MouseButtonPress(MouseButtonInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                foreach (var (key, binding) in button.Bindings)
                {
                    if (binding is MouseControllerButton mbutton && mbutton.Button == info.Button)
                    {
                        mbutton.OnPressed();
                        return true;
                    }
                }
            }

            foreach (var (label, axis) in m_axes)
            {
                foreach (var (key, binding) in axis.Bindings)
                {
                    if (binding is MouseButtonControllerAxis maxis)
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
            }

            return false;
        }

        internal bool Mouse_MouseButtonRelease(MouseButtonInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                foreach (var (key, binding) in button.Bindings)
                {
                    if (binding is MouseControllerButton mbutton && mbutton.Button == info.Button)
                    {
                        mbutton.OnReleased();
                        return true;
                    }
                }
            }

            foreach (var (label, axis) in m_axes)
            {
                foreach (var (key, binding) in axis.Bindings)
                {
                    if (binding is MouseButtonControllerAxis maxis)
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
            }

            return false;
        }

        internal bool Mouse_MouseMove(int x, int y, int dx, int dy)
        {
            bool anyAccepted = false;
            foreach (var (label, axis) in m_axes)
            {
                foreach (var (key, binding) in axis.Bindings)
                {
                    if (binding is MouseMotionControllerAxis maxis)
                    {
                        maxis.Motion(dx, dy);
                        anyAccepted = true;
                    }
                }
            }

            return anyAccepted;
        }

        internal bool Gamepad_Pressed(GamepadButtonInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                foreach (var (key, binding) in button.Bindings)
                {
                    if (binding is GamepadControllerButton gbutton && gbutton.Button == info.Button)
                    {
                        gbutton.OnPressed();
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool Gamepad_Released(GamepadButtonInfo info)
        {
            foreach (var (label, button) in m_buttons)
            {
                foreach (var (key, binding) in button.Bindings)
                {
                    if (binding is GamepadControllerButton gbutton && gbutton.Button == info.Button)
                    {
                        gbutton.OnReleased();
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool Gamepad_AxisMotion(GamepadAxisInfo info)
        {
            foreach (var (label, axis) in m_axes)
            {
                foreach (var (key, binding) in axis.Bindings)
                {
                    if (binding is GamepadAxisControllerAxis gaxis && gaxis.Axis == info.Axis)
                    {
                        gaxis.Motion(info.Value);
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool Gamepad_BallMotion(GamepadBallInfo info)
        {
            return false;
        }
    }

    internal interface IControllerButtonBinding
    {
        public HybridLabel Label { get; set; }

        public bool IsDown { get; }

        public event Action<IControllerButtonBinding, HybridLabel>? Pressed;
        public event Action<IControllerButtonBinding, HybridLabel>? Released;
    }

    internal interface IControllerAxisBinding
    {
        public HybridLabel Label { get; set; }

        public float AxisValue { get; }
#if STORE_AXIS_DELTAS
        // delta is separate bc "radial"/"continuous" axes can loop around and need to report their deltas accordingly.
        public float AxisDelta { get; }
#endif
        public float PartialTick { get; }

        public event Action<IControllerAxisBinding, HybridLabel, float, float>? Changed;
        public event Action<IControllerAxisBinding, HybridLabel, int>? Ticked;

        void Update(float delta);
        void WriteDataToJson(JsonTextWriter writer);
    }

    internal abstract class SimpleButtonTrigger : IControllerButtonBinding
    {
        public HybridLabel Label { get; set; }

        public bool IsDown { get; private set; }

        public event Action<IControllerButtonBinding, HybridLabel>? Pressed;
        public event Action<IControllerButtonBinding, HybridLabel>? Released;

        protected SimpleButtonTrigger()
        {
            Pressed += (a, b) => IsDown = true;
            Released += (a, b) => IsDown = false;
        }

        public void OnPressed() => Pressed?.Invoke(this, Label);
        public void OnReleased() => Released?.Invoke(this, Label);
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

    internal abstract class SimpleAxisButtonTrigger : IControllerAxisBinding
    {
        public HybridLabel Label { get; set; }

        public enum RangeKind
        {
            Single, Double
        }

        public readonly ControllerAxisStyle Style;
        //public readonly RangeKind Kind;

        public float AxisValue { get; private set; }
#if STORE_AXIS_DELTAS
        public float AxisDelta { get; private set; }
#endif

        public float PartialTick { get; private set; }

        public event Action<IControllerAxisBinding, HybridLabel, float, float>? Changed;
        public event Action<IControllerAxisBinding, HybridLabel, int>? Ticked;

        public float Speed = 10.0f;

        private bool m_positive, m_negative;
        private float m_currentValue, m_targetValue;

        /// <summary>
        /// Number of ticks per second.
        /// </summary>
        public float TickRate { get; set; } = 10;

        public SimpleAxisButtonTrigger(ControllerAxisStyle style, RangeKind kind = RangeKind.Single)
        {
            Style = style;
            //Kind = kind;
            Changed += (a, _, value, delta) =>
            {
                AxisValue = value;
#if STORE_AXIS_DELTAS
                AxisDelta = delta;
#endif
            };
        }

        public virtual void WriteDataToJson(JsonTextWriter writer)
        {
            writer.WritePropertyName("style");
            writer.WriteValue($"{Style}");

            writer.WritePropertyName("speed");
            writer.WriteValue(Speed);
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
                    PartialTick -= delta * TickRate;
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

        internal void PressPositive() { if (m_negative != (m_positive = true)) PartialTick = 1; }
        internal void ReleasePositive() => m_positive = false;

        internal void PressNegative() { if ((m_negative = true) != m_positive) PartialTick = -1; }
        internal void ReleaseNegative() => m_negative = false;

        private void OnChanged(float value, float delta) => Changed?.Invoke(this, Label, value, delta);
        private void OnTicked(int direction) => Ticked?.Invoke(this, Label, direction);
    }

    internal sealed class KeyboardControllerAxis : SimpleAxisButtonTrigger
    {
        public readonly KeyCode Positive, Negative;

        public KeyboardControllerAxis(ControllerAxisStyle style, KeyCode positive, KeyCode negative = KeyCode.UNKNOWN)
            : base(style)
        {
            Positive = positive;
            Negative = negative;
        }
    }

    internal sealed class MouseButtonControllerAxis : SimpleAxisButtonTrigger
    {
        public readonly MouseButton Positive, Negative;

        public MouseButtonControllerAxis(ControllerAxisStyle style, MouseButton positive, MouseButton negative = MouseButton.Unknown)
            : base(style)
        {
            Positive = positive;
            Negative = negative;
        }
    }

    internal sealed class MouseMotionControllerAxis : IControllerAxisBinding
    {
        public HybridLabel Label { get; set; }

        public readonly Axis Axis;
        public readonly float Sensitivity;

        public float AxisValue { get; set; }
#if STORE_AXIS_DELTAS
        public float AxisDelta { get; set; }
#endif

        public event Action<IControllerAxisBinding, HybridLabel, float, float>? Changed;
        public event Action<IControllerAxisBinding, HybridLabel, int>? Ticked;

        public float PartialTick { get; private set; }

        public MouseMotionControllerAxis(Axis axis, float sens = 1.0f)
        {
            Axis = axis;
            Sensitivity = sens;
            Changed += (a, _, value, delta) =>
            {
                AxisValue = value;
#if STORE_AXIS_DELTAS
                AxisDelta = delta;
#endif
            };
        }

        public void WriteDataToJson(JsonTextWriter writer)
        {
            writer.WritePropertyName("sens");
            writer.WriteValue(Sensitivity);
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
            Changed?.Invoke(this, Label, value, delta);
        }

        private void OnTicked(int direction) => Ticked?.Invoke(this, Label, direction);
    }

    internal sealed class GamepadAxisControllerAxis : IControllerAxisBinding
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

        public event Action<IControllerAxisBinding, HybridLabel, float, float>? Changed;
        public event Action<IControllerAxisBinding, HybridLabel, int>? Ticked;

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
            Changed += (a, _, value, delta) =>
            {
                AxisValue = value;
#if STORE_AXIS_DELTAS
                AxisDelta = delta;
#endif
            };

            m_axisDeltas = new float[smoothing];
        }

        public void WriteDataToJson(JsonTextWriter writer)
        {
            writer.WritePropertyName("style");
            writer.WriteValue($"{Style}");

            writer.WritePropertyName("sens");
            writer.WriteValue(Sensitivity);

            writer.WritePropertyName("smoothing");
            writer.WriteValue(SampleSmoothing);
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

            Changed?.Invoke(this, Label, value, delta);
        }

        private void OnTicked(int direction) => Ticked?.Invoke(this, Label, direction);
    }
}
