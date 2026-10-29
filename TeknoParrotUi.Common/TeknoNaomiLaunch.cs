using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        public int RenderScale { get; internal set; }
        public bool AutoRenderScale { get; internal set; }
        public ProcessStartInfo StartInfo { get; internal set; }
    }

    // Plans are device-free. Prepare writes only a newly named launch binding file.
    public static class TeknoNaomiLaunch
    {
        public const string AutoResolution = "Auto";

        // Height in physical pixels of the primary display; 0 when unknown. Tests replace it.
        public static Func<int> DisplayHeight { get; set; } = PrimaryDisplayHeight;

        // With nearest presentation a scale above the display only discards rows, so
        // pick the largest scale whose 480 source lines fit the display height. ssaa
        // averages every rendered pixel, so it gets twice that (2x2 supersampling).
        public static int AutoRenderScale(int displayHeight, bool supersampled = false) =>
            displayHeight <= 0 ? 4 : Math.Max(1, Math.Min(8, displayHeight / 480 * (supersampled ? 2 : 1)));

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
            bool Enabled(string name, bool fallback = false)
            {
                var value = Setting(name, fallback ? "1" : "0");
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
            var presentationFilter = Setting("Presentation Filter", "ssaa");
            if (!TeknoNaomiProfileSettings.PresentationFilters.Contains(presentationFilter)) throw new InvalidOperationException("Invalid Presentation Filter.");
            var crt = Setting("CRT Shader", "None");
            if (!TeknoNaomiProfileSettings.CrtShaders.Contains(crt)) throw new InvalidOperationException("Invalid CRT Shader.");
            var resolution = Setting("Internal Resolution", "4");
            var autoScale = resolution == AutoResolution;
            int scale;
            if (autoScale) scale = AutoRenderScale(DisplayHeight?.Invoke() ?? 0, crt == "None" && presentationFilter == "ssaa");
            else if (!int.TryParse(resolution, NumberStyles.None, CultureInfo.InvariantCulture, out scale) || scale < 1 || scale > 8)
                throw new InvalidOperationException("Internal Resolution must be Auto or from 1 to 8.");
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
            args.AddRange(new[] { "--presentation-filter", presentationFilter });
            if (crt != "None") args.AddRange(new[] { "--crt", crt == "Lottes" ? "lottes" : "lottes-downsample" });
            if (Enabled("Use Bezel")) args.Add("--bezels");
            if (Enabled("Cabinet Outputs")) args.Add("--outputs");
            if (Enabled("Free Play")) args.Add("--free-play");
            if (Enabled("Precise SH-4 FPU")) args.Add("--sh4-precise-fpu");
            var bios = Setting("BIOS").Trim();
            if (bios.Length != 0)
            {
                if (!int.TryParse(bios, NumberStyles.None, CultureInfo.InvariantCulture, out var biosId) || biosId > 99)
                    throw new InvalidOperationException("BIOS must be empty or a number from 0 to 99.");
                args.AddRange(new[] { "--bios", biosId.ToString(CultureInfo.InvariantCulture) });
            }
            if (Enabled("Enable VR"))
            {
                string Metres(string name, string fallback)
                {
                    var text = Setting(name, fallback).Trim().Replace(',', '.');
                    if (text.Length == 0) text = fallback;
                    if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var metres) ||
                        metres < 0.25 || metres > 8)
                        throw new InvalidOperationException(name + " must be from 0.25 to 8 metres.");
                    return metres.ToString(CultureInfo.InvariantCulture);
                }
                var filter = Setting("VR Filter", "linear");
                if (filter != "linear" && filter != "nearest") throw new InvalidOperationException("Invalid VR Filter.");
                args.AddRange(new[] { "--vr", "--vr-screen-distance", Metres("VR Screen Distance", "2"),
                    "--vr-screen-width", Metres("VR Screen Width", "2.4"), "--vr-filter", filter });
                if (Enabled("VR Head Locked")) args.Add("--vr-head-locked");
                if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            }
            else if (Enabled("Side-by-Side 3D")) args.Add("--sbs");
            return new TeknoNaomiLaunchPlan { Set = entry.Set, RomArchive = archive, ChdRoot = chdRoot,
                StateRoot = state, Cabinet = cabinet, BindingsPath = bindingPath, BindingsText = bindings,
                RenderScale = scale, AutoRenderScale = autoScale,
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
            log?.Invoke("TeknoNaomi: " + plan.Set + ", cabinet " + plan.Cabinet + ", render scale " + plan.RenderScale +
                (plan.AutoRenderScale ? " (Auto)" : "") + ". Saved bindings exported for this launch.");
            return plan.StartInfo;
        }

        private static int PrimaryDisplayHeight()
        {
            try
            {
                var mode = new DisplayMode { dmSize = (short)Marshal.SizeOf(typeof(DisplayMode)) };
                return EnumDisplaySettings(null, EnumCurrentSettings, ref mode) ? mode.dmPelsHeight : 0;
            }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException)
            {
                return 0;
            }
        }

        private const int EnumCurrentSettings = -1;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNumber, ref DisplayMode mode);

        // DEVMODE, as in MonitorSelection. The current mode is in physical pixels,
        // unaffected by Windows display scaling.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DisplayMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
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
