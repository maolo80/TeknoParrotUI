using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TeknoParrotUi.Common
{
    public sealed class TeknoNaomiLaunchPlan
    {
        public string Set { get; internal set; }
        public string RomArchive { get; internal set; }
        public string ChdRoot { get; internal set; }
        public string StateRoot { get; internal set; }
        public string Cabinet { get; internal set; }
        public string BindingsPath { get; internal set; }
        public string BindingsText { get; internal set; }
        public ProcessStartInfo StartInfo { get; internal set; }
    }

    // Plans are device-free. Prepare writes only a newly named launch binding file.
    public static class TeknoNaomiLaunch
    {
        public static TeknoNaomiLaunchPlan Plan(GameProfile profile, string uiRoot,
            string gameLocation, bool isTest = false)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (isTest) throw new NotSupportedException("Use the saved Test control inside TeknoNaomi; direct Test Menu boot is not implemented.");
            if (profile.ConfigValues?.Any(field => field.FieldName == ArcadeGameRevisions.SettingName) == true &&
                ArcadeGameRevisions.Handles(profile))
                profile = ArcadeGameRevisions.CreateLaunchProfile(profile);
            else
                profile = profile.Clone();
            TeknoNaomiProfileSettings.Normalize(profile);
            var entry = TeknoNaomiCatalog.ForProfile(profile.ProfileName);
            if (entry == null || !string.Equals(profile.ExecutableName, entry.Set + ".zip", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The TeknoNaomi profile and ROM set identities do not match.");
            if (!string.IsNullOrWhiteSpace(profile.CustomArguments) || !string.IsNullOrWhiteSpace(profile.ExtraParameters))
                throw new NotSupportedException("TeknoNaomi launch settings must use the profile's named fields.");
            uiRoot = Path.GetFullPath(uiRoot);
            string Setting(string name, string fallback = "") => profile.ConfigValues?.FirstOrDefault(f => f.FieldName == name)?.FieldValue ?? fallback;
            string Resolve(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(uiRoot, path));
            bool Enabled(string name)
            {
                var value = Setting(name, "0");
                if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                throw new InvalidOperationException("Invalid " + name + " setting.");
            }
            var selected = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            if (string.IsNullOrWhiteSpace(selected)) throw new InvalidOperationException("Select " + entry.Set + ".zip as the game executable.");
            var archive = Resolve(selected);
            if (!string.Equals(Path.GetExtension(archive), ".zip", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select a ROM ZIP archive for this game revision.");
            // A merged game parent locates the media directory; --set retains
            // the exact selected revision. BIOS archives are never game choices.
            var archives = entry.MediaDirectories.Where(set =>
                TeknoNaomiCatalog.ForProfile("teknonaomi_" + set) != null).ToArray();
            if (!archives.Any(set => string.Equals(Path.GetFileName(archive), set + ".zip", StringComparison.OrdinalIgnoreCase)))
            {
                var previous = TeknoNaomiCatalog.ForProfile("teknonaomi_" + Path.GetFileNameWithoutExtension(archive).ToLowerInvariant());
                var sameFamily = previous != null &&
                    (previous.MediaDirectories.Any(set => archives.Contains(set, StringComparer.Ordinal)) ||
                     ArcadeGameRevisions.GetOptions(profile).Any(option => option.Value == "teknonaomi_" + previous.Set));
                var replacement = sameFamily ? archives.Select(set => Path.Combine(Path.GetDirectoryName(archive), set + ".zip"))
                    .FirstOrDefault(File.Exists) : null;
                if (replacement == null)
                    throw new InvalidOperationException("Select " + entry.Set + ".zip or its merged game parent archive.");
                archive = replacement;
            }
            var romRoot = Path.GetDirectoryName(archive);
            var chdRoot = TeknoNaomiProfileSettings.ChdRoot(profile, uiRoot);
            if (entry.Disks.Length > 0 && chdRoot.Length == 0)
                throw new InvalidOperationException("Select CHD Root below the ROM ZIP.");
            if (entry.Disks.Length == 0 && !string.IsNullOrWhiteSpace(profile.GamePath2))
                chdRoot = "";

            var work = Path.Combine(uiRoot, "TeknoNaomi");
            var configuredStateRoot = Setting("Save Root");
            var state = Resolve(string.IsNullOrWhiteSpace(configuredStateRoot) ? Path.Combine(work, "state") : configuredStateRoot);
            var cabinet = TeknoNaomiProfileSettings.CabinetStateId(profile);
            if (!int.TryParse(Setting("Internal Resolution", "4"), NumberStyles.None, CultureInfo.InvariantCulture, out var scale) || scale < 1 || scale > 8)
                throw new InvalidOperationException("Internal Resolution must be from 1 to 8.");
            var display = Setting("DisplayMode", "Windowed");
            if (display != "Windowed" && display != "Fullscreen") throw new InvalidOperationException("Invalid DisplayMode.");
            var bindings = TeknoNaomiInputBindings.Serialize(profile, entry.Set);
            var bindingPath = Path.Combine(state, "bindings", entry.Set, cabinet, Guid.NewGuid().ToString("N") + ".cfg");
            var args = new List<string> { "--set", entry.Set, "--rom-root", romRoot, "--state-root", state,
                "--cabinet", cabinet, "--play", "--sh4-recompiler", "--vulkan-raster", "--render-scale",
                scale.ToString(CultureInfo.InvariantCulture), "--tpui-bindings", bindingPath };
            if (chdRoot.Length != 0) args.AddRange(new[] { "--chd-root", chdRoot });
            if (display == "Fullscreen") args.Add("--fullscreen");
            if (Enabled("Widescreen")) args.Add("--widescreen");
            return new TeknoNaomiLaunchPlan { Set = entry.Set, RomArchive = archive, ChdRoot = chdRoot,
                StateRoot = state, Cabinet = cabinet, BindingsPath = bindingPath, BindingsText = bindings,
                StartInfo = new ProcessStartInfo(Path.Combine(work, "TeknoNaomi.exe"), string.Join(" ", args.Select(Quote)))
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = work, RedirectStandardError = true } };
        }

        public static ProcessStartInfo Prepare(GameProfile profile, string uiRoot, string gameLocation,
            Action<string> log, bool isTest = false)
        {
            var plan = Plan(profile, uiRoot, gameLocation, isTest);
            if (!File.Exists(plan.StartInfo.FileName)) throw new FileNotFoundException("Install the TeknoNaomi runner with saved TPUI binding support.", plan.StartInfo.FileName);
            if (!File.Exists(plan.RomArchive)) throw new FileNotFoundException("The selected ROM archive does not exist.", plan.RomArchive);
            if (plan.ChdRoot.Length > 0 && !Directory.Exists(plan.ChdRoot)) throw new DirectoryNotFoundException("The CHD Root does not exist.");
            Directory.CreateDirectory(Path.GetDirectoryName(plan.BindingsPath));
            using (var file = new FileStream(plan.BindingsPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(file, new UTF8Encoding(false, true))) writer.Write(plan.BindingsText);
            log?.Invoke("TeknoNaomi: " + plan.Set + ", cabinet " + plan.Cabinet + ". Saved bindings exported for this launch.");
            return plan.StartInfo;
        }

        private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

        public static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var c in value)
            {
                if (c == '\\') { ++slashes; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
