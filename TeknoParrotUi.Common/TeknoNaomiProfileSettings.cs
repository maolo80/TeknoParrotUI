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
                f.FieldName == "Mute Audio" || f.FieldName == "Host Timing");
            profile.HasTwoExecutables = HasChd(profile);
            NormalizeCabinet(profile, saved);
        }

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
