using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

internal static class Program
{
    private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(GameProfile));
    private static int checks;
    private static void Require(bool value, string message)
    { ++checks; if (!value) throw new InvalidOperationException(message); }
    private static string Snapshot(GameProfile profile)
    { using (var text = new StringWriter()) { Serializer.Serialize(text, profile); return text.ToString(); } }
    private static string Hash(string path)
    { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))); }
    private static void Setting(GameProfile profile, string name, string value)
    {
        var field = profile.ConfigValues.FirstOrDefault(f => f.FieldName == name);
        if (field == null) { field = new FieldInformation { CategoryName = "Media", FieldName = name }; profile.ConfigValues.Add(field); }
        field.FieldValue = value;
    }
    private static void Reject(string id)
    {
        try { OnlineGameRevisionProfiles.Load(id); }
        catch (ArgumentException) { ++checks; return; }
        throw new InvalidOperationException("Offline/invalid room accepted: " + id);
    }

    private static int Main(string[] args)
    {
        var directory = Directory.GetCurrentDirectory();
        var developer = Environment.GetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES");
        var handoff = Environment.GetEnvironmentVariable("TP_TPONLINE2");
        var stock = ArcadeGameRevisions.StockProfileLoader;
        var user = ArcadeGameRevisions.UserProfileLoader;
        var users = ArcadeGameRevisions.UserProfilesProvider;
        try
        {
            var root = Path.GetFullPath(args[0]);
            Directory.SetCurrentDirectory(Path.Combine(root, "TeknoParrotUi.Common"));
            // Same visibility setting as the retained release candidate launcher.
            Environment.SetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES", "1");
            const string online = "naomi-routing-test|1|Player|2";
            Environment.SetEnvironmentVariable("TP_TPONLINE2", online);
            var paths = Directory.GetFiles("GameProfiles", "teknonaomi_*.xml");
            var hashes = paths.ToDictionary(p => p, Hash);
            var templates = paths.Select(p => stock(Path.GetFileNameWithoutExtension(p))).ToDictionary(p => p.ProfileName);
            var media = Path.Combine(Path.GetFullPath(args[1]), "media");
            if (Directory.Exists(media)) throw new InvalidOperationException("Use a fresh test output directory");
            Directory.CreateDirectory(media);
            foreach (var template in templates.Values)
                File.WriteAllBytes(Path.Combine(media, template.ExecutableName), new byte[0]);
            var saved = new Dictionary<string, GameProfile>();
            ArcadeGameRevisions.StockProfileLoader = id => templates.TryGetValue(id, out var p) ? p : null;
            ArcadeGameRevisions.UserProfileLoader = id => saved.TryGetValue(id, out var p) ? p : null;
            ArcadeGameRevisions.UserProfilesProvider = () => saved.Values;
            var eligible = templates.Values.Where(p => p.HasTpoSupport).ToArray();
            Require(templates.Count == 349 && eligible.Length == 235, "Online profile coverage");
            foreach (var template in eligible)
            {
                saved.Clear();
                var parent = templates[ArcadeGameRevisions.FamilyId(template)].Clone();
                var parentSet = parent.ProfileName.Substring("teknonaomi_".Length);
                parent.GamePath = Path.Combine(media, parentSet + ".zip");
                parent.HasTpoSupport = false; // A saved pre-online profile must not disable the room.
                Setting(parent, "CHD Root", Path.Combine(media, "chds"));
                Setting(parent, "ROM Root", media);
                Setting(parent, ArcadeGameRevisions.SettingName, ArcadeGameRevisions.GetOptions(parent).Last().Value);
                // Runner options are added by Normalize, not by the stock XML.
                parent.ConfigValues.Add(new FieldInformation { CategoryName = "General", FieldName = "Free Play", FieldValue = "1" });
                parent.ConfigValues.Add(new FieldInformation { CategoryName = "General", FieldName = "BIOS", FieldValue = "2" });
                parent.ConfigValues.Add(new FieldInformation { CategoryName = "Video", FieldName = "Side-by-Side 3D", FieldValue = "1" });
                var coin = parent.JoystickButtons.First(b => b.InputMapping == InputMapping.Coin1);
                coin.BindName = "saved online coin";
                saved[parent.ProfileName] = parent;
                var original = Snapshot(parent);
                var resolved = OnlineGameRevisionProfiles.Load(template.ProfileName);
                Require(resolved != null && resolved.ProfileName == template.ProfileName, "Exact room ID " + template.ProfileName);
                Require(ArcadeGameRevisions.ResolveId(resolved) == template.ProfileName, "Saved revision overrode room " + template.ProfileName);
                Require(resolved.GamePath == parent.GamePath && resolved.HasTpoSupport, "Parent path/online flag " + template.ProfileName);
                Require(resolved.JoystickButtons.First(b => b.InputMapping == InputMapping.Coin1).BindName == coin.BindName, "Saved binding lost " + template.ProfileName);
                var plan = TeknoNaomiLaunch.Plan(resolved, root, resolved.GamePath);
                Require(plan.Set == template.ProfileName.Substring("teknonaomi_".Length), "Wrong native --set " + template.ProfileName);
                Require(plan.StartInfo.Arguments.Contains("\"--free-play\"") && plan.StartInfo.Arguments.Contains("\"--bios\" \"2\"") &&
                    plan.StartInfo.Arguments.Contains("\"--sbs\""), "Saved runner options lost " + template.ProfileName);
                Require(Path.GetFileName(plan.StartInfo.FileName) == "TeknoNaomi.exe", "Canonical runtime name");
                Require(!plan.StartInfo.UseShellExecute && plan.StartInfo.CreateNoWindow, "Hidden direct native launch");
                Require(plan.StartInfo.EnvironmentVariables["TP_TPONLINE2"] == online, "Online handoff not inherited");
                Require(Snapshot(parent) == original, "Online launch mutated saved offline settings");
            }
            foreach (var offline in templates.Values.Where(p => !p.HasTpoSupport)) Reject(offline.ProfileName);
            Reject("teknonaomi_hikcheck"); Reject("teknonaomi_unknown"); Reject("teknonaomi_hotd2/../");
            foreach (var path in paths) Require(Hash(path) == hashes[path], "Stock profile written");
            Console.WriteLine("PASS: " + checks + " checks; 235 exact online revisions, 114 offline exclusions, saved controls/paths and TP_TPONLINE2 inheritance; no devices or games launched.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            ArcadeGameRevisions.StockProfileLoader = stock;
            ArcadeGameRevisions.UserProfileLoader = user;
            ArcadeGameRevisions.UserProfilesProvider = users;
            Environment.SetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES", developer);
            Environment.SetEnvironmentVariable("TP_TPONLINE2", handoff);
            Directory.SetCurrentDirectory(directory);
        }
    }
}
