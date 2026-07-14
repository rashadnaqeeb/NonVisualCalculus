using System;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace NonVisualCalculus.Host
{
    /// <summary>
    /// One-time carryover from the mod's old release name, Whirling in Words: the old settings file
    /// and bookmarks file (both under BepInEx/config) move to this mod's names on the first launch
    /// after the rename, so nothing the player configured is lost. Runs before settings bind so the
    /// migrated values are the ones the mod reads.
    /// </summary>
    internal static class LegacyMigration
    {
        private const string OldConfigFile = "com.rashad.whirlinginwords.cfg";
        private const string OldBookmarksFile = "WhirlingInWords.bookmarks.txt";
        private const string NewBookmarksFile = "NonVisualCalculus.bookmarks.txt";

        /// <summary>Move the old release's settings into <paramref name="config"/>'s file and the old
        /// bookmarks file to its new name. A file already present under the new name wins and the old
        /// one is left untouched (it holds nothing the new file doesn't supersede).</summary>
        public static void Run(string configDir, ConfigFile config, ManualLogSource log)
        {
            if (Migrate(Path.Combine(configDir, OldConfigFile), config.ConfigFilePath, log))
                config.Reload();
            Migrate(Path.Combine(configDir, OldBookmarksFile),
                Path.Combine(configDir, NewBookmarksFile), log);
        }

        private static bool Migrate(string oldPath, string newPath, ManualLogSource log)
        {
            try
            {
                if (!File.Exists(oldPath) || File.Exists(newPath)) return false;
                File.Move(oldPath, newPath);
                log.LogInfo($"Migrated {Path.GetFileName(oldPath)} -> {Path.GetFileName(newPath)}");
                return true;
            }
            catch (Exception ex)
            {
                log.LogWarning($"Migrating {oldPath} failed: {ex.Message}");
                return false;
            }
        }
    }
}
