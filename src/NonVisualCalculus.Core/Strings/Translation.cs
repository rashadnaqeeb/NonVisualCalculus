using System;
using System.Collections.Generic;

namespace NonVisualCalculus.Core.Strings
{
    /// <summary>
    /// The loaded translation overriding <see cref="Strings"/>' English defaults: a key-to-value table
    /// plus the plural rule its "_plural" entry names. English is the baked-in fallback per key, so a
    /// partial translation speaks translated where it can and English where it cannot - never silence.
    /// The module's language sync loads a file matching the game language and calls <see cref="Load"/>;
    /// no file means <see cref="Reset"/> (plain English). Engine-free and unit-tested.
    /// </summary>
    public static class Translation
    {
        /// <summary>The reserved file key naming the plural rule (see <see cref="PluralRules"/>).</summary>
        public const string PluralKey = "_plural";

        private static IReadOnlyDictionary<string, string>? _overrides;
        private static Func<int, int> _plural = PluralRules.English;

        /// <summary>The loaded value for a key, or the given English default.</summary>
        internal static string Get(string key, string english)
            => _overrides != null && _overrides.TryGetValue(key, out string value) ? value : english;

        /// <summary>Whether a translation currently overrides this key (so form lookups know whether
        /// the '|'-split value follows the loaded plural rule or the English default's two forms).</summary>
        internal static bool Overrides(string key)
            => _overrides != null && _overrides.ContainsKey(key);

        /// <summary>The plural-form index for a count under the loaded rule.</summary>
        internal static int PluralIndex(int count) => _plural(count);

        /// <summary>What a <see cref="Load"/> accepted and what it had to set aside, for the caller to
        /// log (Core cannot log itself): entries applied, keys the strings table does not define
        /// (typos, or a file from a newer mod), keys with empty values (skipped - an empty string would
        /// silence information), and a "_plural" naming no known rule (English kept).</summary>
        public sealed class Report
        {
            public int Applied;
            public readonly List<string> UnknownKeys = new List<string>();
            public readonly List<string> EmptyKeys = new List<string>();
            public string? UnknownPluralRule;
        }

        /// <summary>Install a parsed translation, replacing any previous one whole (a language switch
        /// never blends two files). Unknown and empty-valued entries are set aside, not applied.</summary>
        public static Report Load(IReadOnlyDictionary<string, string> entries)
        {
            var report = new Report();
            var accepted = new Dictionary<string, string>(entries.Count, StringComparer.Ordinal);
            var plural = PluralRules.English;

            foreach (var entry in entries)
            {
                if (entry.Key == PluralKey)
                {
                    var rule = PluralRules.Resolve(entry.Value);
                    if (rule != null) plural = rule;
                    else report.UnknownPluralRule = entry.Value;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(entry.Value)) { report.EmptyKeys.Add(entry.Key); continue; }
                if (!Strings.DefinesKey(entry.Key)) { report.UnknownKeys.Add(entry.Key); continue; }
                accepted[entry.Key] = entry.Value;
                report.Applied++;
            }

            _overrides = accepted;
            _plural = plural;
            return report;
        }

        /// <summary>Back to the baked-in English (no translation file for the current language).</summary>
        public static void Reset()
        {
            _overrides = null;
            _plural = PluralRules.English;
        }

        /// <summary>
        /// Parse a translation file: one "key = value" per line, splitting at the first '=' (values may
        /// contain '='), '#' lines are comments, blank lines skipped. Malformed lines land in
        /// <paramref name="errors"/> for the caller to log rather than being silently dropped.
        /// </summary>
        public static Dictionary<string, string> ParseFile(string content, out List<string> errors)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            errors = new List<string>();
            string[] lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r').Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) { errors.Add("line " + (i + 1) + ": no 'key = value'"); continue; }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                entries[key] = value;
            }
            return entries;
        }
    }
}
