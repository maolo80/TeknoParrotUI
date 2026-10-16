using System.IO;
using System.Linq;

namespace TeknoParrotUi.Common
{
    /// <summary>
    /// SEGA's amdaemon.exe for the ALLS games whose loader can play its part ("Emulated Daemon", on by default).
    /// Fate/Grand Order Arcade starts its own daemon, so the UI only cares for Soul Reverse: with the option off,
    /// SEGA's daemon is started first from where the game keeps it (tools\amdaemon beside fbb.exe), never asked for.
    /// </summary>
    public static class SegaDaemon
    {
        public const string FieldName = "Emulated Daemon";

        public static bool Emulated(GameProfile profile) =>
            profile?.ConfigValues?.FirstOrDefault(x => x.FieldName == FieldName)?.FieldValue != "0";

        /// <summary>The daemon the UI must start before the game, or null when it starts none.</summary>
        public static string DaemonToStart(GameProfile profile)
        {
            if (profile?.EmulationProfile != EmulationProfile.ALLSSoulReverse || Emulated(profile) ||
                string.IsNullOrWhiteSpace(profile.GamePath))
                return null;
            var gameDirectory = Path.GetDirectoryName(Path.GetFullPath(profile.GamePath));
            return gameDirectory == null ? null : Path.Combine(gameDirectory, "tools", "amdaemon", "amdaemon.exe");
        }
    }
}
