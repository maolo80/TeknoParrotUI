using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TeknoParrotUi.Common
{
    // Saved TPUI digital bindings, independently addressed by source device.
    // The emulator consumer uses this versioned format, not the old global pad profile.
    public static class TeknoNaomiInputBindings
    {
        public const string Format = "teknonaomi.tpui-bindings.v1";
        public const string ExtendedFormat = "teknonaomi.tpui-bindings.v2";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static string Serialize(GameProfile profile, string set,
            IDictionary<InputMapping, string> verifiedDestinations = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrEmpty(set) || set.Length > 32 ||
                set.Any(c => !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_'))
                throw new ArgumentException("A canonical TeknoNaomi set name is required.", nameof(set));

            var apiField = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == "Input API");
            var apiName = apiField?.FieldValue ?? "DirectInput";
            if (!Enum.TryParse(apiName, false, out InputApi api) || !Enum.IsDefined(typeof(InputApi), api))
                throw new InvalidOperationException("Unknown saved Input API: " + apiName);
            if (api == InputApi.RawInputTrackball)
                throw new NotSupportedException("TeknoNaomi trackball bindings need a verified relative-input route.");

            bool di = api == InputApi.DirectInput || api == InputApi.MergedInput;
            bool xi = api == InputApi.XInput || api == InputApi.MergedInput;
            bool ri = api == InputApi.RawInput ||
                (api == InputApi.MergedInput && apiField?.FieldOptions?.Contains("RawInput") == true);
            bool trackball = api == InputApi.MergedInput &&
                apiField?.FieldOptions?.Contains("RawInputTrackball") == true;
            var lines = new List<string>();
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            bool extended = false;

            foreach (var button in profile.JoystickButtons ?? new List<JoystickButtons>())
            {
                if (button == null) throw new InvalidOperationException("A saved control is null.");
                if (!SettingVisibility.IsVisible(profile.ConfigValues, button.VisibleWhen, button.VisibleWhenValue)) continue;
                bool buttonDi = di && !button.HideWithDirectInput;
                bool buttonXi = xi && !button.HideWithXInput;
                bool buttonRi = ri && !button.HideWithRawInput;
                if (!buttonDi && !buttonXi && !buttonRi) continue;
                var control = button.TeknoNaomiControl ?? TeknoNaomiControlCatalog.Find(profile.ProfileName, button.InputMapping);
                if (control == "p1.keyboard" || control == "p2.keyboard")
                {
                    extended = true;
                    var raw = button.RawInputButton;
                    if (buttonRi && raw != null && raw.DeviceType != RawDeviceType.None)
                    {
                        if (raw.DeviceType != RawDeviceType.Keyboard)
                            throw new InvalidOperationException("Bind any key on the keyboard to use for " + button.ButtonName + ".");
                        ValidateRawPath(raw);
                        Add(lines, control, "raw_keyboard", raw.DevicePath, "keyboard", 0, 0);
                    }
                    else Add(lines, control, "none", "", "none", 0, 0);
                    continue;
                }
                if (button.InputMapping == InputMapping.P1LightGun || button.InputMapping == InputMapping.P2LightGun)
                {
                    if (!buttonRi) continue;
                    extended = true;
                    var player = button.InputMapping == InputMapping.P1LightGun ? "p1" : "p2";
                    var raw = button.RawInputButton;
                    if (raw != null && raw.DeviceType != RawDeviceType.None && raw.DeviceType != RawDeviceType.Mouse)
                        throw new InvalidOperationException("Select a mouse or light gun for " + button.ButtonName + ".");
                    for (int axis = 0; axis < 2; ++axis)
                    {
                        var target = player + ".axis" + axis;
                        // MergedInput can also carry an independently saved pad axis.
                        if (raw != null && raw.DeviceType == RawDeviceType.Mouse)
                        {
                            ValidateRawPath(raw);
                            Add(lines, target, "raw_mouse", raw.DevicePath, axis == 0 ? "pointer_x" : "pointer_y", 6 + axis, 1);
                        }
                    }
                    continue;
                }
                string destination;
                if (verifiedDestinations == null || !verifiedDestinations.TryGetValue(button.InputMapping, out destination))
                    destination = control ?? StandardDestination(button.InputMapping);
                if (!ValidDestination(destination))
                    throw new NotSupportedException("No verified TeknoNaomi digital destination for " + button.InputMapping + ".");
                bool continuous = AxisDestination(destination);
                extended |= continuous || destination.StartsWith("mahjong.", StringComparison.Ordinal) || destination.EndsWith("+", StringComparison.Ordinal) || destination.EndsWith("-", StringComparison.Ordinal);
                if (button.AnalogType != AnalogType.None && !continuous)
                    throw new NotSupportedException("No axis destination for " + button.ButtonName + ".");
                if (!destinations.Add(destination))
                    throw new InvalidOperationException("Duplicate saved destination: " + destination);
                var first = lines.Count;
                if (buttonDi) DirectInput(lines, destination, button.DirectInputButton, continuous);
                if (buttonXi) XInput(lines, destination, button.XInputButton, continuous, button.AnalogType);
                if (trackball && button.RawInputButton != null && button.RawInputButton.DeviceType != RawDeviceType.None)
                    throw new NotSupportedException("Merged trackball bindings require a verified relative-input route.");
                if (buttonRi && !continuous) RawInput(lines, destination, button.RawInputButton);
                if (lines.Count == first) Add(lines, destination, "none", "", "none", 0, 0);
            }
            lines.Sort(StringComparer.Ordinal);
            // Omit unbound axis placeholders if another declaration supplies it.
            var bound = new HashSet<string>(lines.Where(line => !line.Contains("|none|-"))
                .Select(line => line.Substring(0, line.IndexOf('|'))), StringComparer.Ordinal);
            lines.RemoveAll(line => line.Contains("|none|-") && bound.Contains(line.Substring(0, line.IndexOf('|'))));
            var text = "format=" + (extended ? ExtendedFormat : Format) + "\nset=" + set + "\napi=" + api + "\nraw_enabled=" + (ri ? "1" : "0") + "\n" +
                string.Join("\n", lines) + (lines.Count == 0 ? "" : "\n");
            if (lines.Count > 508 || Utf8.GetByteCount(text) > 65536)
                throw new InvalidOperationException("Saved TeknoNaomi bindings exceed the transport limit.");
            return text;
        }

        private static string StandardDestination(InputMapping mapping)
        {
            switch (mapping)
            {
                case InputMapping.Test: return "test";
                case InputMapping.Coin1: return "p1.coin";
                case InputMapping.Coin2: return "p2.coin";
                case InputMapping.Service1: return "p1.service";
                case InputMapping.Service2: return "p2.service";
                case InputMapping.JvsTwoCoin1: return "p3.coin";
                case InputMapping.JvsTwoCoin2: return "p4.coin";
                case InputMapping.JvsTwoService1: return "p3.service";
                case InputMapping.JvsTwoService2: return "p4.service";
                case InputMapping.ExtensionOne17: return "p1.button7";
                case InputMapping.ExtensionOne18: return "p1.button8";
            }
            var name = mapping.ToString();
            for (var player = 1; player <= 2; ++player)
            {
                var prefix = "P" + player + "Button";
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var suffix = name.Substring(prefix.Length);
                if (suffix == "Up" || suffix == "Down" || suffix == "Left" || suffix == "Right" || suffix == "Start")
                    return "p" + player + "." + suffix.ToLowerInvariant();
                if (suffix.Length == 1 && suffix[0] >= '1' && suffix[0] <= '6')
                    return "p" + player + ".button" + suffix;
            }
            if (name.StartsWith("JvsTwo", StringComparison.Ordinal) &&
                Enum.TryParse(name.Substring(6), out InputMapping primary))
            {
                var destination = StandardDestination(primary);
                if (destination != null && destination.StartsWith("p1.", StringComparison.Ordinal))
                    return "p3." + destination.Substring(3);
                if (destination != null && destination.StartsWith("p2.", StringComparison.Ordinal))
                    return "p4." + destination.Substring(3);
            }
            return null;
        }

        private static bool ValidDestination(string value)
        {
            if (value == "test") return true;
            if (value != null && value.StartsWith("mahjong.", StringComparison.Ordinal))
            {
                var key = value.Substring(8);
                return (key.Length == 1 && key[0] >= 'a' && key[0] <= 'n') ||
                    new[] { "kan", "pon", "chi", "reach", "ron", "bet", "lastchance", "flipflop" }.Contains(key);
            }
            if (AxisDestination(value)) return true;
            if (value != null && value.Length == 9 && (value[8] == '+' || value[8] == '-') && AxisDestination(value.Substring(0, 8))) return true;
            if (value == null || value.Length < 5 || value[0] != 'p' || value[1] < '1' || value[1] > '4' || value[2] != '.') return false;
            var control = value.Substring(3);
            return new[] { "up", "down", "left", "right", "start", "coin", "service" }.Contains(control) ||
                (control.Length == 7 && control.StartsWith("button", StringComparison.Ordinal) && control[6] >= '1' && control[6] <= '8');
        }

        private static bool AxisDestination(string value) => value != null && value.Length == 8 &&
            value[0] == 'p' && value[1] >= '1' && value[1] <= '4' && value.Substring(2, 5) == ".axis" && value[7] >= '0' && value[7] <= '5';

        private static void XInput(List<string> lines, string destination, XInputButton button, bool continuous = false, AnalogType type = AnalogType.None)
        {
            if (button == null) return;
            var axes = new[] { button.IsLeftThumbX, button.IsLeftThumbY, button.IsRightThumbX,
                button.IsRightThumbY, button.IsLeftTrigger, button.IsRightTrigger };
            var selected = axes.Count(x => x) + (button.IsButton ? 1 : 0);
            if (selected == 0 && button.ButtonCode == 0) return;
            if (button.XInputIndex < 0 || button.XInputIndex > 3 || selected > 1)
                throw new InvalidOperationException("Invalid or ambiguous saved XInput control.");
            var id = button.XInputIndex.ToString(CultureInfo.InvariantCulture);
            if (selected == 0 || button.IsButton)
            {
                if (continuous) throw new InvalidOperationException("Bind an axis for " + destination + ".");
                var mask = unchecked((ushort)button.ButtonCode);
                const int validMask = 0xf3ff;
                if (mask == 0 || (mask & ~validMask) != 0 || (mask & (mask - 1)) != 0)
                    throw new InvalidOperationException("Saved XInput button must be one defined button bit.");
                Add(lines, destination, "xinput", id, "button", mask, 0);
                return;
            }
            var names = new[] { "left_x", "left_y", "right_x", "right_y", "left_trigger", "right_trigger" };
            var axis = Array.FindIndex(axes, x => x);
            if (continuous)
            {
                bool pedal = axis < 4 && (type == AnalogType.Gas || type == AnalogType.Brake);
                Add(lines, destination, "xinput", id, names[axis], pedal ? 1 : 0,
                    pedal ? (button.IsAxisMinus ? -1 : 1) :
                    type == AnalogType.AnalogJoystickReverse || type == AnalogType.SWThrottleReverse ? -1 : 1);
                return;
            }
            Add(lines, destination, "xinput", id, names[axis], axis < 4 ? 15000 : 0,
                axis < 4 && button.IsAxisMinus ? -1 : 1);
        }

        private static void DirectInput(List<string> lines, string destination, JoystickButton button, bool continuous = false)
        {
            if (button == null || button.JoystickGuid == Guid.Empty) return;
            var id = button.JoystickGuid.ToString("D").ToLowerInvariant();
            if (continuous)
            {
                if (!button.IsAxis || button.Button < 0 || button.Button > 28 || button.Button % 4 != 0)
                    throw new InvalidOperationException("Bind a DirectInput axis for " + destination + ".");
                Add(lines, destination, "directinput", id, "axis", button.Button, button.IsReverseAxis ? -1 : 1);
                return;
            }
            if (button.IsFullAxis || button.IsReverseAxis)
                throw new NotSupportedException("Saved full/reversed DirectInput axes need a verified continuous route.");
            if (button.IsAxis)
            {
                if (button.Button < 0 || button.Button > 28 || button.Button % 4 != 0)
                    throw new NotSupportedException("DirectInput axis offset is not a supported basic axis.");
                Add(lines, destination, "directinput", id, "axis", button.Button, button.IsAxisMinus ? -1 : 1);
            }
            else if (button.Button >= 32 && button.Button <= 44 && button.Button % 4 == 0)
            {
                if (button.PovDirection != 0 && button.PovDirection != 9000 && button.PovDirection != 18000 && button.PovDirection != 27000)
                    throw new InvalidOperationException("Invalid saved DirectInput POV direction.");
                Add(lines, destination, "directinput", id, "pov", button.Button, button.PovDirection);
            }
            else
            {
                if (button.Button < 48 || button.Button > 175)
                    throw new NotSupportedException("DirectInput button offset is not supported.");
                Add(lines, destination, "directinput", id, "button", button.Button, 0);
            }
        }

        private static void RawInput(List<string> lines, string destination, RawInputButton button)
        {
            if (button == null || button.DeviceType == RawDeviceType.None) return;
            ValidateRawPath(button);
            if (button.DeviceType == RawDeviceType.Keyboard)
            {
                var key = (int)button.KeyboardKey;
                if (key < 1 || key > 255)
                    throw new NotSupportedException("Raw keyboard binding needs one virtual key, without modifier flags.");
                Add(lines, destination, "raw_keyboard", button.DevicePath, "key", key, 0);
            }
            else if (button.DeviceType == RawDeviceType.Mouse)
            {
                var key = (int)button.MouseButton;
                if (key == 0) return;
                if (key < 1 || key > 5) throw new InvalidOperationException("Invalid raw mouse button.");
                Add(lines, destination, "raw_mouse", button.DevicePath, "button", key, 0);
            }
            else throw new NotSupportedException("Unsupported saved raw-input device type.");
        }

        private static void ValidateRawPath(RawInputButton button)
        {
            if (string.IsNullOrEmpty(button.DevicePath) || Utf8.GetByteCount(button.DevicePath) > 4096 || button.DevicePath.Any(char.IsControl))
                throw new InvalidOperationException("A saved raw-input device path is required.");
        }

        private static void Add(List<string> lines, string destination, string source, string id,
            string control, int argument, int value)
        {
            var bytes = Utf8.GetBytes(id);
            var encoded = bytes.Length == 0 ? "-" : string.Concat(bytes.Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
            lines.Add("bind=" + destination + "|" + source + "|" + encoded + "|" + control + "|" +
                argument.ToString(CultureInfo.InvariantCulture) + "|" + value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
