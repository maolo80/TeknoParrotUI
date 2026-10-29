using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TeknoParrotUi.Common
{
    public static class TeknoNaomiProfileSettings
    {
        public static bool HasChd(GameProfile profile) =>
            profile?.EmulatorType == EmulatorType.TeknoNaomi &&
            TeknoNaomiCatalog.ForProfile(profile.ProfileName)?.Disks.Length > 0;

        // Called by the stock-profile migration and revision selector. Retain the
        // old media location while removing obsolete launch toggles from the UI.
        public static void Normalize(GameProfile profile, GameProfile saved = null)
        {
            if (profile?.EmulatorType != EmulatorType.TeknoNaomi) return;
            if (HasChd(profile) && string.IsNullOrWhiteSpace(profile.GamePath2))
                profile.GamePath2 = (saved ?? profile).ConfigValues?
                    .FirstOrDefault(f => f.FieldName == "CHD Root")?.FieldValue ?? "";
            profile.ConfigValues?.RemoveAll(f => f.FieldName == "ROM Root" || f.FieldName == "CHD Root" ||
                f.FieldName == "Mute Audio" || f.FieldName == "Host Timing" || UnsupportedOfficialFields.Contains(f.FieldName));
            profile.HasTwoExecutables = HasChd(profile);
            NormalizeCabinet(profile, saved);
            AddRunnerOptions(profile);
        }

        // Fields of later official profiles (TPUI 1.0.0.2267) whose launch handling
        // this TPUI does not have; shown, they would do nothing.
        private static readonly string[] UnsupportedOfficialFields = { "Region", "Outputs", "Use Keyboard/Button For Axis" };

        // Runner options that predate their profile fields. Missing fields are added
        // with defaults, so stock and saved profiles gain them without a revision bump.
        internal static void AddRunnerOptions(GameProfile profile)
        {
            if (profile.ConfigValues == null) profile.ConfigValues = new List<FieldInformation>();
            void Add(string category, string name, FieldType type, string value, string hint, params string[] options)
            {
                var field = profile.ConfigValues.FirstOrDefault(f => f.FieldName == name);
                if (field == null)
                    profile.ConfigValues.Add(field = new FieldInformation { CategoryName = category, FieldName = name, FieldValue = value });
                field.FieldType = type;
                field.Hint = hint;
                if (options.Length > 0) field.FieldOptions = options.ToList();
            }
            var resolution = profile.ConfigValues.FirstOrDefault(f => f.FieldName == "Internal Resolution");
            if (resolution?.FieldOptions != null)
            {
                if (!resolution.FieldOptions.Contains(TeknoNaomiLaunch.AutoResolution))
                    resolution.FieldOptions.Insert(0, TeknoNaomiLaunch.AutoResolution);
                resolution.Hint = "Auto fits the primary display: the largest scale whose 480 lines fit it (4 at 2160p), " +
                    "doubled with the ssaa Presentation Filter (8 at 2160p), which averages the extra pixels. " +
                    "With nearest, scales above the display only discard pixels.";
            }
            Add("General", "Cabinet Outputs", FieldType.Bool, "1",
                "Publish lamps and light gun recoil to MAMEOutput clients. Steering forces are not part of these outputs.");
            Add("Video", "Presentation Filter", FieldType.Dropdown, "ssaa",
                "Scaling of the rendered image to the window. ssaa averages every rendered pixel and suits high Internal Resolution; " +
                "bicubic and lanczos are sharper; linear is the cheapest smooth filter; nearest keeps hard pixels.",
                PresentationFilters);
            Add("Video", "CRT Shader", FieldType.Dropdown, "None",
                "CRT-Lottes scanlines and shadow mask. Lottes keeps the Internal Resolution; Lottes Downsample first resolves to native pixels. " +
                "Replaces the Presentation Filter.", CrtShaders);
            Add("Video", "Use Bezel", FieldType.Bool, "0",
                "Show TeknoNaomi/bezels/SET.png around the game, else the parent set's PNG, else default.png. Not used with Widescreen.");
            Add("General", "Free Play", FieldType.Bool, "0", "Set the BIOS coin setting to FREE PLAY.");
            Add("General", "BIOS", FieldType.Text, "",
                "Empty selects the default BIOS. A number selects another BIOS, for example a different region.");
            Add("General", "Precise SH-4 FPU", FieldType.Bool, "0",
                "Keep exact SH-4 floating-point status and exceptional values. Slower; try it for physics or geometry glitches.");
            Add("Video", "Side-by-Side 3D", FieldType.Bool, "0", "Render both eyes of each scene and show them side by side.");
            Add("Video", "Enable VR", FieldType.Bool, "0",
                "Show the game on a virtual screen in an OpenXR headset. The window mirrors the left eye; hold Menu for settings.");
            Add("Video", "Use VR Controls", FieldType.Bool, "1", "Play with the Touch controllers while VR is enabled.");
            Add("Video", "VR Screen Distance", FieldType.Text, "2", "Virtual screen distance in metres, 0.25 to 8.");
            Add("Video", "VR Screen Width", FieldType.Text, "2.4", "Virtual screen width in metres, 0.25 to 8.");
            Add("Video", "VR Head Locked", FieldType.Bool, "0", "Keep the virtual screen fixed to the headset.");
            Add("Video", "VR Filter", FieldType.Dropdown, "linear", "Scaling into the headset image.", "linear", "nearest");
            Add("Force Feedback", "Force Feedback Device", FieldType.DynamicDropdown, "off",
                "Wheel for steering forces, or a controller for vibration. Numbers count devices of that kind in connection order.");
            Add("Force Feedback", "Player 2 Force Feedback Device", FieldType.DynamicDropdown, "off",
                "Independent device for the second player.");
            Add("Force Feedback", "Force Feedback Strength", FieldType.Slider, "100",
                "Overall effect strength. 100% keeps the normal full strength.");
            var strength = profile.ConfigValues.First(f => f.FieldName == "Force Feedback Strength");
            strength.FieldMin = 0; strength.FieldMax = 100; strength.SettingsPage = "ForceFeedback";
            foreach (var device in profile.ConfigValues.Where(f => f.FieldName == "Force Feedback Device" || f.FieldName == "Player 2 Force Feedback Device"))
                device.DynamicOptions = FfbDevices(device.FieldValue);
        }

        // The runner opens SDL3 devices by kind and index; it has no device list to probe.
        internal static readonly System.Text.RegularExpressions.Regex FfbDeviceToken =
            new System.Text.RegularExpressions.Regex("^(wheel|haptic|gamepad):[0-9]{1,2}$");

        private static List<DynamicDropdownOption> FfbDevices(string saved)
        {
            var options = new List<DynamicDropdownOption> { new DynamicDropdownOption { DisplayName = "Force feedback off", Value = "off" } };
            foreach (var kind in new[] { new[] { "gamepad", "Controller {0} (vibration)" }, new[] { "wheel", "Wheel {0}" }, new[] { "haptic", "Haptic device {0}" } })
                for (int index = 0; index < 4; ++index)
                    options.Add(new DynamicDropdownOption
                    {
                        DisplayName = string.Format(CultureInfo.InvariantCulture, kind[1], index + 1),
                        Value = kind[0] + ":" + index.ToString(CultureInfo.InvariantCulture)
                    });
            if (!string.IsNullOrWhiteSpace(saved) && options.All(o => o.Value != saved))
                options.Add(new DynamicDropdownOption { DisplayName = "Previously selected device - " + saved, Value = saved });
            return options;
        }

        internal static readonly string[] PresentationFilters = { "nearest", "linear", "bicubic", "lanczos", "ssaa" };
        internal static readonly string[] CrtShaders = { "None", "Lottes", "Lottes Downsample" };

        public static int CabinetCount(GameProfile profile) =>
            TeknoNaomiCatalog.ForProfile(profile?.ProfileName)?.Cabinets ?? 1;

        private static int LegacyCabinetNumber(string value, int maximum)
        {
            if (value == null) return 1;
            var suffix = value.StartsWith("tpui-", StringComparison.Ordinal) ? value.Substring(5) :
                value.StartsWith("cabinet-", StringComparison.Ordinal) ? value.Substring(8) : value;
            return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
                number >= 1 && number <= maximum ? number : 1;
        }

        private static void NormalizeCabinet(GameProfile profile, GameProfile saved)
        {
            if (profile.ConfigValues == null) profile.ConfigValues = new List<FieldInformation>();
            var field = profile.ConfigValues.FirstOrDefault(f => f.FieldName == "Cabinet ID");
            var previous = (saved ?? profile).ConfigValues?.FirstOrDefault(f => f.FieldName == "Cabinet ID");
            var maximum = CabinetCount(profile);
            if (saved?.TeknoNaomiCabinetStateId != null)
                profile.TeknoNaomiCabinetStateId = saved.TeknoNaomiCabinetStateId;
            // Retain custom old save directories without exposing their names as
            // cabinet numbers. The alias survives profile save/reload and cloning.
            if (previous?.FieldType == FieldType.Text && !string.IsNullOrEmpty(previous.FieldValue))
            {
                profile.TeknoNaomiCabinetStateId = previous.FieldValue;
                if (field != null) field.FieldValue = LegacyCabinetNumber(previous.FieldValue, maximum).ToString(CultureInfo.InvariantCulture);
            }
            if (maximum == 1)
            {
                profile.ConfigValues.RemoveAll(f => f.FieldName == "Cabinet ID");
                return;
            }
            if (field == null)
            {
                field = new FieldInformation { CategoryName = "General", FieldName = "Cabinet ID", FieldValue = "1" };
                profile.ConfigValues.Add(field);
            }
            field.FieldType = FieldType.Dropdown;
            field.FieldOptions = Enumerable.Range(1, maximum).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList();
            field.Hint = maximum > 1
                ? "Select this cabinet's saved settings. TPOnline assigns the link position automatically."
                : "Saved settings for this cabinet.";
        }

        public static string CabinetStateId(GameProfile profile)
        {
            var value = profile.ConfigValues?.FirstOrDefault(f => f.FieldName == "Cabinet ID")?.FieldValue ?? "1";
            var maximum = CabinetCount(profile);
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1 || number > maximum)
                throw new InvalidOperationException("Cabinet ID must be from 1 to " + maximum + ".");
            var legacy = profile.TeknoNaomiCabinetStateId;
            var cabinet = legacy != null && LegacyCabinetNumber(legacy, maximum) == number
                ? legacy : "tpui-" + number.ToString(CultureInfo.InvariantCulture);
            if (cabinet.Length == 0 || cabinet.Length > 64 || cabinet == "." || cabinet == ".." ||
                cabinet.Any(c => !(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-' && c != '_' && c != '.'))
                throw new InvalidOperationException("The saved cabinet identity is invalid.");
            return cabinet;
        }

        public static string ChdRoot(GameProfile profile, string uiRoot)
        {
            var selected = profile.GamePath2;
            if (string.IsNullOrWhiteSpace(selected))
                selected = profile.ConfigValues?.FirstOrDefault(f => f.FieldName == "CHD Root")?.FieldValue;
            if (string.IsNullOrWhiteSpace(selected)) return "";
            var full = Path.GetFullPath(Path.IsPathRooted(selected) ? selected : Path.Combine(uiRoot, selected));
            if (!string.Equals(Path.GetExtension(full), ".chd", StringComparison.OrdinalIgnoreCase)) return full;
            var entry = TeknoNaomiCatalog.ForProfile(profile.ProfileName);
            if (entry == null || !entry.Disks.Contains(Path.GetFileName(full), StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected CHD filename does not belong to this revision.");
            var folder = Path.GetDirectoryName(full);
            return entry.MediaDirectories.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(folder) : folder;
        }
    }
}
