using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ParrotPatcher;

internal static class Program
{
    private static int assertions;

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string originalDirectory = Environment.CurrentDirectory;
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.CurrentDirectory = root;
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            File.WriteAllText("user-profile.txt", "preserve");
            using (var form = new Form1())
            {
                // Create a handle for production Invoke calls without showing the patcher window.
                IntPtr unused = form.Handle;
                var install = typeof(Form1).GetMethod("ProcessSingleZip", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(install != null, "Production ZIP installer not found");
                foreach (string component in new[] { "TeknoMVS", "TeknoCPS", "TeknoSS32", "TeknoTZero", "TeknoNaomi" })
                {
                    string installedExe = Path.Combine(component, component + ".exe");
                    Require(!File.Exists(installedExe), "Fixture must start with no installed executable");
                    Install(form, install, component, "1.0.0.100", "first install");
                    Require(File.Exists(installedExe), component + " executable missing from TPUI component folder");
                    Require(File.ReadAllText(installedExe) == "first install", component + " executable missing from TPUI component folder");
                    Require(File.ReadAllText(Path.Combine(component, "SDL3.dll")) == "runtime first install", component + " runtime dependency missing");
                    Require(File.ReadAllText(Path.Combine(component, "data", "catalog.txt")) == "catalog first install", component + " nested support file missing");
                    Require(!File.Exists(component + ".exe"), component + " executable incorrectly installed in TPUI root");
                    Require(!Directory.Exists(Path.Combine(component, component)), component + " package installed into a duplicate component folder");
                    File.WriteAllText(Path.Combine(component, "user-settings.txt"), "keep settings");
                    Install(form, install, component, "1.0.0.101", "updated install");
                    Require(File.ReadAllText(installedExe) == "updated install", component + " executable not replaced during update");
                    Require(File.ReadAllText(Path.Combine(component, "SDL3.dll")) == "runtime updated install", component + " runtime dependency not updated");
                    Require(File.ReadAllText(Path.Combine(component, "data", "catalog.txt")) == "catalog updated install", component + " support file not updated");
                    Require(File.ReadAllText(Path.Combine(component, "user-settings.txt")) == "keep settings", component + " unrelated settings changed");
                    Require(Directory.GetFiles(component, component + ".exe", SearchOption.AllDirectories).Length == 1, component + " installed executable duplicated");
                }
                if (args.Length == 2) CheckNaomiPackage(form, install, args[0], args[1]);
                else Require(args.Length == 0, "Supply both the TPUI assembly and Naomi package paths");
            }
            Require(File.ReadAllText("user-profile.txt") == "preserve", "Unrelated TPUI root file changed");
            Console.WriteLine("PASS: " + assertions + " assertions; all four real patcher fresh-install and update paths preserve executables, dependencies, subfolders and user files. No downloads or games started.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    private static void CheckNaomiPackage(Form1 form, MethodInfo install, string uiAssembly, string archive)
    {
        string previousDirectory = Environment.CurrentDirectory;
        string realPackageRoot = Path.Combine(previousDirectory, "real-naomi");
        Directory.CreateDirectory(realPackageRoot);
        Environment.CurrentDirectory = realPackageRoot;
        try
        {
            // Load the actual UI registration without constructing a window or starting TPUI.
            var ui = Assembly.LoadFrom(uiAssembly);
            var uiComponents = (IEnumerable)ui.GetType("TeknoParrotUi.MainWindow", true)
                .GetField("components", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var patcherRegistry = typeof(Form1).GetField("uc", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            var patcherComponents = (IEnumerable)patcherRegistry.GetType().GetField("components").GetValue(patcherRegistry);
            var registrations = new[] { uiComponents, patcherComponents }.Select(components => components.Cast<object>()
                .Single(component => (string)Property(component, "name") == "TeknoNaomi")).ToArray();
            foreach (var component in registrations)
            {
                Require((string)Property(component, "location") == Path.Combine("TeknoNaomi", "TeknoNaomi.exe"), "Naomi executable path");
                Require((string)Property(component, "folderOverride") == "TeknoNaomi", "Naomi install directory");
                Require((string)Property(component, "fullUrl") == "https://github.com/teknogods/TeknoParrot/", "Naomi release repository");
                Require(!(bool)Property(component, "manualVersion"), "Naomi uses the normal executable resource version");
                Require(!(bool)Property(component, "opensource"), "Naomi release changelog routing");
            }

            string updateVersion;
            using (var stream = File.OpenRead(archive))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                Require(zip.GetEntry("TeknoNaomi.exe") != null, "Real Naomi package must contain the executable at ZIP root");
                Require(zip.GetEntry("NOTICE.md") != null && zip.GetEntry("release-manifest.json") != null, "Real Naomi package notices and manifest");
                Require(!zip.Entries.Any(entry => entry.FullName.StartsWith("TeknoNaomi/", StringComparison.Ordinal)), "No duplicate component folder in package");
                using (var reader = new StreamReader(zip.GetEntry("release-manifest.json").Open()))
                {
                    var match = Regex.Match(reader.ReadToEnd(), "\"update_version\"\\s*:\\s*\"(\\d+\\.\\d+\\.\\d+\\.\\d+)\"");
                    Require(match.Success, "Numeric update version missing from package manifest");
                    updateVersion = match.Groups[1].Value;
                }
            }
            Directory.CreateDirectory("cache");
            var cachedZip = Path.Combine("cache", "TeknoNaomi" + updateVersion + ".zip");
            File.Copy(archive, cachedZip, true);
            Require(!Directory.Exists("TeknoNaomi"), "Real package must begin with a fresh installation");
            install.Invoke(form, new object[] { cachedZip });
            var installed = Path.Combine("TeknoNaomi", "TeknoNaomi.exe");
            var info = FileVersionInfo.GetVersionInfo(installed);
            Version numeric;
            Require(Version.TryParse(info.FileVersion, out numeric) && numeric.Revision >= 0, "Four-part Windows file version");
            Require(info.FileVersion == updateVersion, "Package manifest and installed version must agree");
            Require(info.ProductVersion == info.FileVersion, "Product and file versions must use the same numeric updater format");
            foreach (var component in registrations)
            {
                component.GetType().GetField("_localVersion").SetValue(component, null);
                Require((string)Property(component, "localVersion") == info.ProductVersion, "Package version must be read through the normal updater path");
            }
            File.WriteAllText(Path.Combine("TeknoNaomi", "user-settings.txt"), "keep settings");
            File.WriteAllText(installed, "old executable fixture");
            install.Invoke(form, new object[] { cachedZip });
            Require(FileVersionInfo.GetVersionInfo(installed).FileVersion == updateVersion, "Real update replaces an existing executable");
            Require(File.ReadAllText(Path.Combine("TeknoNaomi", "user-settings.txt")) == "keep settings", "Real package preserves user settings");
            Require(!File.Exists("TeknoNaomi.exe") && Directory.GetFiles("TeknoNaomi", "TeknoNaomi.exe", SearchOption.AllDirectories).Length == 1,
                "Real package installs exactly one executable at the configured location");
            Console.WriteLine("Real Naomi ZIP installed through ParrotPatcher; both compiled registrations report " + info.FileVersion + " for " + info.ProductVersion + ".");
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
        }
    }

    private static object Property(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);

    private static void Install(Form1 form, MethodInfo install, string component, string version, string payload)
    {
        Directory.CreateDirectory("cache");
        string archive = Path.Combine("cache", component + version + ".zip");
        using (var stream = File.Create(archive))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            Add(zip, component + ".exe", payload);
            Add(zip, "SDL3.dll", "runtime " + payload);
            zip.CreateEntry("data/");
            Add(zip, "data/catalog.txt", "catalog " + payload);
        }
        // Use the same ZIP recognition and extraction entry point as a downloaded update.
        install.Invoke(form, new object[] { archive });
    }

    private static void Add(ZipArchive zip, string name, string payload)
    {
        using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)))
            writer.Write(payload);
    }
}
