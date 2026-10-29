using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

internal static class Program
{
    private static int Main(string[] args)
    {
        try { ReleaseChecks.Run(args[0], args[1]); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

internal static class ReleaseChecks
{
    private static int checks;
    private static void Require(bool ok, string message)
    { ++checks; if (!ok) throw new InvalidOperationException(message); }
    private static GameProfile Load(string path)
    {
        using (var stream = File.OpenRead(path))
            return (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(stream);
    }
    private static void GunDevices(Dictionary<string, GameProfile> profiles, string output)
    {
        var gunSets = new[] { "braveff", "claychal", "confmiss", "deathcox", "deathcoxj", "deathcoxo", "hotd2", "hotd2e", "hotd2o", "hotd2p", "lupinsho", "lupinshoo", "mok", "ninjaslt", "ninjaslta", "ninjasltj", "ninjasltu", "rangrmsn", "sprtshot", "xtrmhnt2", "xtrmhunt" };
        var directory = Path.Combine(output, "gun-bindings");
        Directory.CreateDirectory(directory);
        foreach (var set in gunSets)
        foreach (var api in new[] { "RawInput", "MergedInput" })
        {
            var p = profiles["teknonaomi_" + set].Clone();
            p.ConfigValues.First(f => f.FieldName == "Input API").FieldValue = api;
            for (int player = 1; player <= 2; ++player)
            {
                var mapping = player == 1 ? InputMapping.P1LightGun : InputMapping.P2LightGun;
                var device = p.JoystickButtons.Single(b => b.InputMapping == mapping);
                Require(!device.HideWithRawInput && device.HideWithDirectInput && device.HideWithXInput,
                    set + " exposes the RawInput device selector for player " + player);
                device.RawInputButton = new RawInputButton { DeviceType = RawDeviceType.Mouse, DevicePath = "gun" + player };
                device.BindNameRi = "Gun " + player;
                p.JoystickButtons.Single(b => b.InputMapping == (player == 1 ? InputMapping.P1Button1 : InputMapping.P2Button1)).RawInputButton =
                    new RawInputButton { DeviceType = RawDeviceType.Mouse, DevicePath = "gun" + player, MouseButton = RawMouseButton.LeftButton };
            }
            var exported = TeknoNaomiInputBindings.Serialize(p, set);
            for (int player = 1; player <= 2; ++player)
            {
                var prefix = "bind=p" + player;
                var source = "|raw_mouse|67756e3" + player;
                Require(exported.Contains(prefix + ".axis0" + source + "|pointer_x|6|1") &&
                    exported.Contains(prefix + ".axis1" + source + "|pointer_y|7|1") &&
                    exported.Contains(prefix + ".button1" + source + "|button|1|0"),
                    set + " " + api + " exports independent aim and trigger for player " + player);
            }
            var serializer = new XmlSerializer(typeof(GameProfile));
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, p); stream.Position = 0;
                p = (GameProfile)serializer.Deserialize(stream);
            }
            Require(TeknoNaomiInputBindings.Serialize(p, set) == exported, set + " saved gun devices survive XML save/reload");
            if (api == "RawInput") File.WriteAllText(Path.Combine(directory, set + ".cfg"), exported);
        }
    }

    private static void RunnerOptions(GameProfile stock, string root)
    {
        string Arguments(GameProfile p) => TeknoNaomiLaunch.Plan(p, root, p.GamePath).StartInfo.Arguments;
        void Set(GameProfile p, string name, string value) => p.ConfigValues.Single(f => f.FieldName == name).FieldValue = value;
        var p0 = stock.Clone();
        TeknoNaomiProfileSettings.Normalize(p0);
        var names = new[] { "Cabinet Outputs", "Free Play", "BIOS", "Precise SH-4 FPU", "Side-by-Side 3D", "Enable VR",
            "Use VR Controls", "VR Screen Distance", "VR Screen Width", "VR Head Locked", "VR Filter",
            "Presentation Filter", "CRT Shader", "Use Bezel",
            "Force Feedback Device", "Player 2 Force Feedback Device", "Force Feedback Strength" };
        Require(names.All(n => p0.ConfigValues.Count(f => f.FieldName == n) == 1), "Runner option fields are added once");
        TeknoNaomiProfileSettings.Normalize(p0);
        Require(names.All(n => p0.ConfigValues.Count(f => f.FieldName == n) == 1), "Runner option fields survive a second normalization");
        var defaults = Arguments(p0);
        Require(defaults.Contains("\"--outputs\"") && defaults.Contains("\"--presentation-filter\" \"ssaa\"") &&
            !new[] { "--free-play", "--bios", "--sh4-precise-fpu", "--sbs", "--vr", "--crt", "--bezels", "--ffb" }.Any(defaults.Contains),
            "Runner option defaults");
        Set(p0, "Presentation Filter", "lanczos"); Set(p0, "CRT Shader", "Lottes Downsample"); Set(p0, "Use Bezel", "1");
        var presentation = Arguments(p0);
        Require(presentation.Contains("\"--presentation-filter\" \"lanczos\"") && presentation.Contains("\"--crt\" \"lottes-downsample\"") &&
            presentation.Contains("\"--bezels\""), "Presentation options");
        Set(p0, "CRT Shader", "Lottes");
        Require(Arguments(p0).Contains("\"--crt\" \"lottes\""), "CRT Lottes");
        Set(p0, "Presentation Filter", "ssaa"); Set(p0, "CRT Shader", "None"); Set(p0, "Use Bezel", "0");
        var device = p0.ConfigValues.Single(f => f.FieldName == "Force Feedback Device");
        var strength = p0.ConfigValues.Single(f => f.FieldName == "Force Feedback Strength");
        Require(device.FieldType == FieldType.DynamicDropdown && device.DynamicOptions.Any(o => o.Value == "gamepad:0") &&
            device.DynamicOptions.Any(o => o.Value == "wheel:0") && strength.FieldType == FieldType.Slider && strength.FieldMax == 100 &&
            strength.SettingsPage == "ForceFeedback", "Force feedback fields");
        Set(p0, "Force Feedback Device", "gamepad:0"); Set(p0, "Force Feedback Strength", "70");
        var ffb = Arguments(p0);
        Require(ffb.Contains("\"--ffb-device\" \"gamepad:0\"") && ffb.Contains("\"--ffb-gain\" \"70\"") && !ffb.Contains("--ffb-device2"),
            "Force feedback device and strength");
        Set(p0, "Force Feedback Device", "off"); Set(p0, "Player 2 Force Feedback Device", "wheel:1");
        ffb = Arguments(p0);
        Require(ffb.Contains("\"--ffb-device2\" \"wheel:1\"") && !ffb.Contains("\"--ffb-device\"") && ffb.Contains("\"--ffb-gain\" \"70\""),
            "Second player force feedback device");
        Set(p0, "Player 2 Force Feedback Device", "off");
        Set(p0, "Force Feedback Device", "haptic:12");
        TeknoNaomiProfileSettings.Normalize(p0);
        Require(device.DynamicOptions.Any(o => o.Value == "haptic:12"), "Saved device outside the list stays selectable");
        Set(p0, "Force Feedback Device", "off");
        Set(p0, "Free Play", "1"); Set(p0, "Precise SH-4 FPU", "1"); Set(p0, "BIOS", "2"); Set(p0, "Cabinet Outputs", "0");
        Set(p0, "Side-by-Side 3D", "1");
        var enabled = Arguments(p0);
        Require(enabled.Contains("\"--free-play\"") && enabled.Contains("\"--sh4-precise-fpu\"") &&
            enabled.Contains("\"--bios\" \"2\"") && enabled.Contains("\"--sbs\"") && !enabled.Contains("--outputs"), "Runner options enabled");
        Set(p0, "Enable VR", "1"); Set(p0, "VR Screen Distance", "1,5"); Set(p0, "Use VR Controls", "0"); Set(p0, "VR Head Locked", "1");
        var vr = Arguments(p0);
        Require(vr.Contains("\"--vr\" \"--vr-screen-distance\" \"1.5\" \"--vr-screen-width\" \"2.4\" \"--vr-filter\" \"linear\"") &&
            vr.Contains("\"--vr-head-locked\"") && vr.Contains("\"--no-vr-controls\"") && !vr.Contains("--sbs"), "VR options");
        var resolution = p0.ConfigValues.Single(f => f.FieldName == "Internal Resolution");
        Require(resolution.FieldOptions.Count(o => o == "Auto") == 1 && resolution.FieldOptions[0] == "Auto" && resolution.FieldValue == "4",
            "Auto resolution is offered once without changing the default");
        var display = TeknoNaomiLaunch.DisplayHeight;
        try
        {
            Set(p0, "Internal Resolution", "Auto");
            foreach (var pair in new[] { new[] { 2160, 8 }, new[] { 1440, 6 }, new[] { 1080, 4 }, new[] { 720, 2 }, new[] { 4320, 8 }, new[] { 0, 4 } })
            {
                TeknoNaomiLaunch.DisplayHeight = () => pair[0];
                Require(TeknoNaomiLaunch.Plan(p0, root, p0.GamePath).RenderScale == pair[1], "Supersampled Auto resolution for " + pair[0] + " lines");
            }
            TeknoNaomiLaunch.DisplayHeight = () => 2160;
            Set(p0, "CRT Shader", "Lottes");
            Require(TeknoNaomiLaunch.Plan(p0, root, p0.GamePath).RenderScale == 4, "CRT shader does not supersample Auto");
            Set(p0, "CRT Shader", "None");
            Set(p0, "Presentation Filter", "nearest");
            foreach (var pair in new[] { new[] { 2160, 4 }, new[] { 1440, 3 }, new[] { 1080, 2 }, new[] { 1200, 2 }, new[] { 720, 1 },
                new[] { 300, 1 }, new[] { 4320, 8 }, new[] { 0, 4 } })
            {
                TeknoNaomiLaunch.DisplayHeight = () => pair[0];
                var plan = TeknoNaomiLaunch.Plan(p0, root, p0.GamePath);
                Require(plan.RenderScale == pair[1] && plan.AutoRenderScale &&
                    plan.StartInfo.Arguments.Contains("\"--render-scale\" \"" + pair[1] + "\""), "Auto resolution for " + pair[0] + " lines");
            }
            TeknoNaomiLaunch.DisplayHeight = () => throw new InvalidOperationException("Display queried for a fixed scale");
            Set(p0, "Internal Resolution", "3");
            Require(TeknoNaomiLaunch.Plan(p0, root, p0.GamePath).RenderScale == 3, "Fixed resolution ignores the display");
        }
        finally { TeknoNaomiLaunch.DisplayHeight = display; }
        foreach (var invalid in new[] { new[] { "Force Feedback Device", "wheel" }, new[] { "Force Feedback Device", "joystick:0" },
            new[] { "Force Feedback Device", "gamepad:0;--mute" }, new[] { "Presentation Filter", "SSAA" }, new[] { "CRT Shader", "lottes" }, new[] { "Internal Resolution", "auto" }, new[] { "Internal Resolution", "9" }, new[] { "BIOS", "-1" }, new[] { "BIOS", "x" }, new[] { "VR Screen Width", "9" }, new[] { "VR Filter", "cubic" } })
        {
            var p = p0.Clone();
            Set(p, invalid[0], invalid[1]);
            var rejected = false;
            try { Arguments(p); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Rejects " + invalid[0] + " = " + invalid[1]);
        }
    }

    // Official profiles after TPUI 1.0.0.2267 drop TeknoNaomiControl and add fields
    // this TPUI cannot launch with. Bindings must export as from the stock profiles.
    private static void OfficialProfiles(IEnumerable<GameProfile> stock)
    {
        foreach (var original in stock)
        {
            var set = original.ProfileName.Substring("teknonaomi_".Length);
            var official = original.Clone();
            foreach (var button in official.JoystickButtons) button.TeknoNaomiControl = null;
            Require(TeknoNaomiInputBindings.Serialize(official, set) == TeknoNaomiInputBindings.Serialize(original, set),
                "Official-style profile exports the stock bindings: " + original.ProfileName);
            official.ConfigValues.Add(new FieldInformation { CategoryName = "General", FieldName = "Region", FieldValue = "Korea" });
            official.ConfigValues.Add(new FieldInformation { CategoryName = "General", FieldName = "Outputs", FieldValue = "Network Outputs" });
            official.ConfigValues.Add(new FieldInformation { CategoryName = "General", FieldName = "Use Keyboard/Button For Axis", FieldValue = "1" });
            TeknoNaomiProfileSettings.Normalize(official);
            Require(!official.ConfigValues.Any(f => f.FieldName == "Outputs" || f.FieldName == "Use Keyboard/Button For Axis"),
                "Unsupported official fields are not shown: " + original.ProfileName);
            var regions = TeknoNaomiProfileSettings.RegionOptions(official);
            var region = official.ConfigValues.SingleOrDefault(f => f.FieldName == "Region");
            Require(regions == null ? region == null : region != null && region.FieldOptions.SequenceEqual(regions) &&
                region.FieldValue == (regions.Contains("Korea") ? "Korea" : "Auto"),
                "Region only for Atomiswave and Hikaru, with their own countries: " + original.ProfileName);
        }
    }

    private static void Regions(Dictionary<string, GameProfile> profiles, string root)
    {
        string Arguments(GameProfile p) => TeknoNaomiLaunch.Plan(p, root, p.GamePath).StartInfo.Arguments;
        Require(profiles.Values.Count(p => TeknoNaomiProfileSettings.RegionOptions(p) != null) == 42, "34 Atomiswave and 8 Hikaru sets");
        foreach (var pair in new[] { new[] { "teknonaomi_mslug6", "Other", "other" }, new[] { "teknonaomi_braveff", "Korea", "korea" } })
        {
            var p = profiles[pair[0]].Clone();
            TeknoNaomiProfileSettings.Normalize(p);
            Require(!Arguments(p).Contains("--region"), "Auto region passes nothing: " + pair[0]);
            p.ConfigValues.Single(f => f.FieldName == "Region").FieldValue = pair[1];
            Require(Arguments(p).Contains("\"--region\" \"" + pair[2] + "\""), "Region option: " + pair[0]);
        }
        var aw = profiles["teknonaomi_mslug6"].Clone();
        TeknoNaomiProfileSettings.Normalize(aw);
        aw.ConfigValues.Single(f => f.FieldName == "Region").FieldValue = "Korea";
        var rejected = false;
        try { Arguments(aw); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected || !Arguments(aw).Contains("--region"), "Atomiswave has no Korea region");
    }

    public static void Run(string root, string output)
    {
        var common = Path.Combine(Path.GetFullPath(root), "TeknoParrotUi.Common");
        var profiles = Directory.GetFiles(Path.Combine(common, "GameProfiles"), "teknonaomi_*.xml")
            .Select(Load).ToDictionary(p => p.ProfileName);
        Require(profiles.Count == 349, "Complete Naomi catalog");
        var media = Path.Combine(Path.GetFullPath(output), "release-media");
        Directory.CreateDirectory(media);
        int analog = 0, disks = 0;
        foreach (var p in profiles.Values)
        {
            Require(!p.DevOnly && p.Patreon, "Release visibility and subscriber policy: " + p.ProfileName);
            p.GamePath = Path.Combine(media, p.ExecutableName);
            if (p.HasTwoExecutables) { p.GamePath2 = Path.Combine(media, "chds"); ++disks; }
            Require(!p.ConfigValues.Any(f => new[] { "ROM Root", "CHD Root", "Host Timing", "Mute Audio" }.Contains(f.FieldName)), "Obsolete field: " + p.ProfileName);
            var cabinet = p.ConfigValues.SingleOrDefault(f => f.FieldName == "Cabinet ID");
            var count = TeknoNaomiProfileSettings.CabinetCount(p);
            Require(count == 1 ? cabinet == null : cabinet != null && cabinet.FieldType == FieldType.Dropdown && cabinet.FieldValue == "1" &&
                cabinet.FieldOptions.SequenceEqual(Enumerable.Range(1, count).Select(n => n.ToString())),
                "Cabinet selector only for linked cabinets: " + p.ProfileName);
            var management = new[] { InputMapping.Test, InputMapping.Service1, InputMapping.Coin1, InputMapping.Coin2, InputMapping.P1ButtonStart,
                InputMapping.Service2, InputMapping.P2ButtonStart, InputMapping.JvsTwoService1, InputMapping.JvsTwoService2,
                InputMapping.JvsTwoCoin1, InputMapping.JvsTwoCoin2, InputMapping.JvsTwoP1ButtonStart, InputMapping.JvsTwoP2ButtonStart };
            var expected = management.Where(m => p.JoystickButtons.Any(b => b.InputMapping == m)).ToArray();
            if (p.GameVersion == "braveff")
                expected = new[] { InputMapping.Test, InputMapping.Service1, InputMapping.Service2,
                    InputMapping.Coin1, InputMapping.Coin2, InputMapping.P1ButtonStart, InputMapping.P2ButtonStart };
            Require(p.JoystickButtons.Take(expected.Length).Select(b => b.InputMapping).SequenceEqual(expected),
                "Test, Service, Coins, Start order: " + p.ProfileName);
            var meta = File.ReadAllText(Path.Combine(common, "Metadata", p.ProfileName + ".json"));
            Require(meta.Contains(p.ExecutableName) && !new[] { "unverified", "untested", "Development profile", "saved TPUI controls reach", "current follow-up" }.Any(meta.Contains), "Metadata: " + p.ProfileName);
            Require(new[] { "nvidia", "amd", "intel" }.All(v => meta.Contains("\"" + v + "\": \"OK\"")), "GPU compatibility: " + p.ProfileName);
            if (p.JoystickButtons.Any(b => b.AnalogType != AnalogType.None)) ++analog;
            Require(p.JoystickButtons.Select(b => b.InputMapping).Distinct().Count() == p.JoystickButtons.Count, "Ambiguous mapping: " + p.ProfileName);
            foreach (var b in p.JoystickButtons.Where(b => b.AnalogType != AnalogType.None))
                Require(b.TeknoNaomiControl != null && b.TeknoNaomiControl.Length == 8 && !b.HideWithXInput && !b.HideWithDirectInput, "Missing continuous route: " + p.ProfileName + ":" + b.ButtonName);
            // Old saved options must never re-enable muting or unthrottled play.
            p.ConfigValues.Add(new FieldInformation { FieldName = "Host Timing", FieldValue = "Uncapped" });
            p.ConfigValues.Add(new FieldInformation { FieldName = "Mute Audio", FieldValue = "1" });
            var plan = TeknoNaomiLaunch.Plan(p, root, p.GamePath);
            Require(plan.StartInfo.Arguments.Contains("--play") && !plan.StartInfo.Arguments.Contains("--uncapped") &&
                !plan.StartInfo.Arguments.Contains("--mute"), "Audio/timing defaults: " + p.ProfileName);
            Require(plan.StartInfo.FileName.EndsWith("TeknoNaomi.exe"), "Runtime filename");
            Require(plan.Cabinet == "tpui-1", "Default cabinet retains existing saves: " + p.ProfileName);
        }
        Require(analog == 126 && disks > 0, "Continuous controls and CHD coverage");
        RunnerOptions(profiles["teknonaomi_f355"], root);
        OfficialProfiles(profiles.Values);
        Regions(profiles, root);
        foreach (var id in new[] { "teknonaomi_sgnascar", "teknonaomi_sgnascaro" })
        {
            var p = profiles[id];
            Require(p.JoystickButtons.Single(b => b.TeknoNaomiControl == "p1.axis0").AnalogType == AnalogType.Wheel, "NASCAR steering is an axis");
            Require(p.JoystickButtons.Single(b => b.TeknoNaomiControl == "p1.axis5").AnalogType == AnalogType.Gas, "NASCAR gas is an axis");
            Require(p.JoystickButtons.Single(b => b.TeknoNaomiControl == "p1.axis4").AnalogType == AnalogType.Brake, "NASCAR brake is an axis");
        }
        foreach (var id in new[] { "samba", "sambaa", "sambap", "samba2k" })
            Require(profiles["teknonaomi_" + id].JoystickButtons.Count(b => b.AnalogType != AnalogType.None) == 8, "Two independent maraca pairs: " + id);
        foreach (var p in profiles.Values)
            foreach (var b in p.JoystickButtons.Where(b => b.TeknoNaomiControl != null && (b.TeknoNaomiControl.EndsWith(".axis4") || b.TeknoNaomiControl.EndsWith(".axis5"))))
                Require(b.AnalogType == AnalogType.Gas || b.AnalogType == AnalogType.Brake, "Zero-neutral demand: " + p.ProfileName + ":" + b.ButtonName);
        var brave = profiles["teknonaomi_braveff"];
        Require(brave.JoystickButtons.Single(b => b.InputMapping == InputMapping.P1ButtonStart).ButtonName == "Player 1 Start" &&
            brave.JoystickButtons.Single(b => b.InputMapping == InputMapping.P2ButtonStart).ButtonName == "Player 2 Start" &&
            brave.JoystickButtons.Single(b => b.InputMapping == InputMapping.Service2).ButtonName == "Service 2",
            "Brave uses consistent player and service names");
        Require(!brave.JoystickButtons.Any(b => b.InputMapping == InputMapping.P1Button3 || b.InputMapping == InputMapping.P2Button3),
            "Brave off-screen sensors are not exposed as reload buttons");
        Require(brave.JoystickButtons.Count(b => b.AnalogType != AnalogType.None) == 4, "Independent two-nozzle X/Y axes");
        Require(brave.JoystickButtons.Where(b => b.ButtonName.Contains("Aim ")).All(b => b.ButtonName.EndsWith("(Digital)")), "Digital aim alternatives labelled");
        var confmiss = profiles["teknonaomi_confmiss"].Clone();
        var saved = confmiss.Clone(); saved.GamePath2 = "";
        saved.ConfigValues.Add(new FieldInformation { FieldName = "CHD Root", FieldValue = Path.Combine(media, "old-chds") });
        confmiss.GamePath2 = "";
        TeknoNaomiProfileSettings.Normalize(confmiss, saved);
        Require(confmiss.GamePath2.EndsWith("old-chds") && !confmiss.ConfigValues.Any(f => f.FieldName == "Host Timing" || f.FieldName == "Mute Audio"), "Old CHD root migration");
        confmiss.GamePath2 = Path.Combine(media, "chds", "confmiss", "gds-0001.chd");
        Require(TeknoNaomiProfileSettings.ChdRoot(confmiss, root) == Path.Combine(media, "chds"), "Old exact disc remains usable");
        Require(TeknoNaomiProfileSettings.CabinetCount(profiles["teknonaomi_initd"]) == 2 &&
            TeknoNaomiProfileSettings.CabinetCount(profiles["teknonaomi_alienfnt"]) == 4 &&
            TeknoNaomiProfileSettings.CabinetCount(profiles["teknonaomi_clubkrt"]) == 8 &&
            TeknoNaomiProfileSettings.CabinetCount(profiles["teknonaomi_vonot"]) == 3 &&
            TeknoNaomiProfileSettings.CabinetCount(profiles["teknonaomi_hotd2"]) == 1,
            "Per-game link capacities; local players do not become cabinets");
        foreach (var oldId in new[] { "tpui-1", "tpui-3", "cabinet-2", "2", "my-cabinet" })
        {
            var p = profiles["teknonaomi_alienfnt"].Clone();
            var old = p.Clone();
            var field = old.ConfigValues.Single(f => f.FieldName == "Cabinet ID");
            field.FieldType = FieldType.Text; field.FieldValue = oldId;
            TeknoNaomiProfileSettings.Normalize(p, old);
            field = p.ConfigValues.Single(f => f.FieldName == "Cabinet ID");
            Require(field.FieldType == FieldType.Dropdown && field.FieldOptions.Contains(field.FieldValue), "Migrate old cabinet selector: " + oldId);
            var serializer = new XmlSerializer(typeof(GameProfile));
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, p); stream.Position = 0;
                p = (GameProfile)serializer.Deserialize(stream);
            }
            Require(TeknoNaomiLaunch.Plan(p, root, p.GamePath).Cabinet == oldId, "Save/reload retains old cabinet directory: " + oldId);
            var originalNumber = p.ConfigValues.Single(f => f.FieldName == "Cabinet ID").FieldValue;
            p.ConfigValues.Single(f => f.FieldName == "Cabinet ID").FieldValue = "4";
            Require(TeknoNaomiLaunch.Plan(p, root, p.GamePath).Cabinet == "tpui-4", "Another cabinet has separate saves");
            p.ConfigValues.Single(f => f.FieldName == "Cabinet ID").FieldValue = originalNumber;
            Require(TeknoNaomiLaunch.Plan(p, root, p.GamePath).Cabinet == oldId, "Returning to original cabinet retains alias");
        }
        foreach (var invalid in new[] { "0", "5", "-1", "tpui-1", "../outside" })
        {
            var p = profiles["teknonaomi_alienfnt"].Clone();
            p.ConfigValues.Single(f => f.FieldName == "Cabinet ID").FieldValue = invalid;
            bool rejected = false;
            try { TeknoNaomiLaunch.Plan(p, root, p.GamePath); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Reject invalid cabinet number: " + invalid);
        }
        foreach (var oldId in new[] { "tpui-1", "cabinet-1", "my-nozzle-cabinet" })
        {
            var p = profiles["teknonaomi_braveff"].Clone();
            var old = p.Clone();
            old.ConfigValues.Add(new FieldInformation { FieldName = "Cabinet ID", FieldType = FieldType.Text, FieldValue = oldId });
            TeknoNaomiProfileSettings.Normalize(p, old);
            Require(!p.ConfigValues.Any(f => f.FieldName == "Cabinet ID"), "Single cabinet has no selector");
            var serializer = new XmlSerializer(typeof(GameProfile));
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, p); stream.Position = 0;
                p = (GameProfile)serializer.Deserialize(stream);
            }
            TeknoNaomiProfileSettings.Normalize(p);
            Require(TeknoNaomiLaunch.Plan(p, root, p.GamePath).Cabinet == oldId, "Single-cabinet save alias survives reload: " + oldId);
        }
        GunDevices(profiles, output);
        Console.WriteLine("Naomi release checks=" + checks + "; profiles=349; analog profiles=" + analog + "; CHD profiles=" + disks);
    }
}
