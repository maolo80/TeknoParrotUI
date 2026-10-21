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
