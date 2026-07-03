using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using I2.Loc;

namespace DiscoAccess.Module
{
    /// <summary>
    /// Keeps the mod's authored strings in the game's language: watches I2's current language and loads
    /// the matching translation file from the plugin's lang folder into Core's <see cref="Translation"/>
    /// table; no file means the baked-in English. A file is found by the I2 language code ("fr.txt") or
    /// the lowercased language name with word runs hyphenated ("french.txt", "portuguese-brazil.txt").
    /// Checked every frame (one string compare) because the player can quick-switch the game language in
    /// play (Ctrl+L) as well as in the options menu; authored strings resolve through the table at speak
    /// time, so speech after a switch is current with no reload.
    /// </summary>
    internal sealed class LanguageSync
    {
        private readonly IModHost _host;
        // The last language applied and the last failure logged: change markers so the per-frame check
        // is one compare and a persistent failure logs once, not sixty times a second.
        private string _applied;
        private string _loggedFailure;

        public LanguageSync(IModHost host)
        {
            _host = host;
            Tick();
        }

        public void Tick()
        {
            string language;
            try
            {
                language = LocalizationManager.CurrentLanguage;
            }
            catch (Exception e)
            {
                LogFailureOnce("reading the game language failed: " + e.Message);
                return;
            }
            if (string.IsNullOrEmpty(language) || language == _applied)
                return;
            _applied = language;
            Apply(language);
        }

        private void Apply(string language)
        {
            try
            {
                string dir = Path.Combine(
                    Path.GetDirectoryName(typeof(Translation).Assembly.Location) ?? "", "lang");
                string file = FindFile(dir, language);
                if (file == null)
                {
                    Translation.Reset();
                    _host.LogInfo($"language '{language}': no translation file in {dir}, speaking English");
                    return;
                }

                var entries = Translation.ParseFile(File.ReadAllText(file), out List<string> errors);
                var report = Translation.Load(entries);
                _host.LogInfo($"language '{language}': loaded {report.Applied} strings from {Path.GetFileName(file)}");
                foreach (string error in errors)
                    _host.LogWarning($"translation {Path.GetFileName(file)}: {error}");
                if (report.UnknownKeys.Count > 0)
                    _host.LogWarning($"translation {Path.GetFileName(file)}: unknown keys ignored: "
                                     + string.Join(", ", report.UnknownKeys));
                if (report.EmptyKeys.Count > 0)
                    _host.LogWarning($"translation {Path.GetFileName(file)}: empty values ignored: "
                                     + string.Join(", ", report.EmptyKeys));
                if (report.UnknownPluralRule != null)
                    _host.LogWarning($"translation {Path.GetFileName(file)}: unknown {Translation.PluralKey} "
                                     + $"rule '{report.UnknownPluralRule}', keeping English plurals");
            }
            catch (Exception e)
            {
                // A failed load keeps whatever table was live (worst case English) - never half-applied.
                _host.LogError($"loading the '{language}' translation failed: {e}");
            }
        }

        // The translation file for a language: by I2 code first (the stable short form), then by the
        // sanitized language name. Null when neither exists (the shipped state for most languages).
        private static string FindFile(string dir, string language)
        {
            string code = LocalizationManager.CurrentLanguageCode;
            if (!string.IsNullOrEmpty(code))
            {
                string byCode = Path.Combine(dir, code.ToLowerInvariant() + ".txt");
                if (File.Exists(byCode)) return byCode;
            }
            string byName = Path.Combine(dir, SanitizeName(language) + ".txt");
            return File.Exists(byName) ? byName : null;
        }

        // A language name as a file stem: lowercased, letter/digit runs kept, everything else folded to
        // single hyphens ("Portuguese (Brazil)" to "portuguese-brazil").
        private static string SanitizeName(string language)
        {
            var sb = new StringBuilder(language.Length);
            foreach (char c in language)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            while (sb.Length > 0 && sb[sb.Length - 1] == '-') sb.Length--;
            return sb.ToString();
        }

        private void LogFailureOnce(string message)
        {
            if (message == _loggedFailure) return;
            _loggedFailure = message;
            _host.LogWarning(message);
        }
    }
}
