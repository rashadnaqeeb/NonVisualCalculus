using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NonVisualCalculus.Core.Strings;
using Xunit;

namespace NonVisualCalculus.Tests
{
    /// <summary>
    /// Structural validation of every committed lang/*.txt - the checks that make a translation
    /// mechanically wrong rather than stylistically debatable. A dropped {0} silently deletes
    /// information from speech and a foreign {9} throws at speak time, so both fail the build here;
    /// wording stays the translator's business.
    /// </summary>
    public class LanguageFileTests
    {
        [Fact]
        public void CommittedLanguageFiles_AreStructurallySound()
        {
            var failures = new List<string>();
            foreach (string path in LanguageFiles())
                Validate(path, failures);
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        private static IEnumerable<string> LanguageFiles()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NonVisualCalculus.slnx")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Directory.GetFiles(Path.Combine(dir!.FullName, "lang"), "*.txt");
        }

        // The table's English defaults, keyed for slot and form comparison (the dumped template is
        // the public rendering of the internal table).
        private static readonly Dictionary<string, string> English = LoadEnglish();

        private static Dictionary<string, string> LoadEnglish()
        {
            var map = Translation.ParseFile(Strings.DumpTemplate(), out List<string> errors);
            Assert.Empty(errors);
            map.Remove(Translation.PluralKey);
            return map;
        }

        private static void Validate(string path, List<string> failures)
        {
            string file = Path.GetFileName(path);
            void Fail(string key, string problem) => failures.Add(file + ": " + key + ": " + problem);

            string content = File.ReadAllText(path);
            var entries = Translation.ParseFile(content, out List<string> parseErrors);
            foreach (string error in parseErrors) Fail("(parse)", error);
            foreach (string dup in DuplicateKeys(content)) Fail(dup, "defined more than once (the last silently wins)");

            entries.TryGetValue(Translation.PluralKey, out string? ruleName);
            Func<int, int>? rule = PluralRules.Resolve(ruleName ?? "english");
            if (rule == null)
            {
                Fail(Translation.PluralKey, "unknown rule '" + ruleName + "'");
                rule = PluralRules.English;
            }
            int maxForms = MaxFormCount(rule);

            foreach (var entry in entries)
            {
                string key = entry.Key, value = entry.Value;
                if (key == Translation.PluralKey) continue;
                if (!English.TryGetValue(key, out string? english))
                {
                    Fail(key, "not a key the strings table defines");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(value))
                {
                    Fail(key, "empty value (would silence information)");
                    continue;
                }

                HashSet<int> englishSlots = Slots(english);
                string[] forms = value.Split('|');

                if (!english.Contains('|'))
                {
                    if (forms.Length > 1)
                    {
                        Fail(key, "'|' in a single-form value would be spoken literally");
                        continue;
                    }
                    CheckSlots(key, value, englishSlots, requireAll: true, Fail);
                }
                else if (key == "WorldCompass")
                {
                    // List semantics, not plurals: a missing bearing falls back to English per entry,
                    // so fewer than eight means untranslated directions, and more are unreachable.
                    if (forms.Length != 8)
                        Fail(key, "the compass needs exactly 8 bearings, got " + forms.Length);
                }
                else if (key.StartsWith("ContainerWord_", StringComparison.Ordinal))
                {
                    // Picked singular/plural by the dev name, not by the plural rule.
                    if (forms.Length != 2)
                        Fail(key, "container words are 'singular|plural', got " + forms.Length + " form(s)");
                    foreach (string form in forms)
                        CheckSlots(key, form, englishSlots, requireAll: false, Fail);
                }
                else
                {
                    // A plural key: fewer forms than the rule selects is legal (selection clamps to
                    // the last form), more can never be spoken. Only the last form - the clamp target
                    // and the general "other" case - must carry every slot; a one/two form may drop
                    // the number naturally ("an hour").
                    if (forms.Length > maxForms)
                        Fail(key, "has " + forms.Length + " forms but the '" + (ruleName ?? "english")
                            + "' rule selects at most " + maxForms + " (unreachable forms - wrong _plural?)");
                    for (int i = 0; i < forms.Length; i++)
                        CheckSlots(key, forms[i], englishSlots, requireAll: i == forms.Length - 1, Fail);
                }
            }
        }

        private static readonly Regex SlotPattern = new Regex(@"\{(\d+)(?::[^}]*)?\}", RegexOptions.Compiled);

        private static HashSet<int> Slots(string value)
        {
            var set = new HashSet<int>();
            foreach (Match m in SlotPattern.Matches(value))
                set.Add(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            return set;
        }

        private static void CheckSlots(string key, string form, HashSet<int> englishSlots,
                                       bool requireAll, Action<string, string> fail)
        {
            HashSet<int> slots = Slots(form);
            foreach (int s in slots)
                if (!englishSlots.Contains(s))
                    fail(key, "slot {" + s + "} does not exist in the English value (throws at speak time)");
            if (requireAll)
                foreach (int s in englishSlots)
                    if (!slots.Contains(s))
                        fail(key, "slot {" + s + "} from the English value is missing (drops spoken information)");
        }

        // Keys that appear more than once, which ParseFile resolves last-wins with no report.
        private static IEnumerable<string> DuplicateKeys(string content)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var dupes = new List<string>();
            foreach (string raw in content.Split('\n'))
            {
                string line = raw.TrimEnd('\r').Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                if (!seen.Add(key)) dupes.Add(key);
            }
            return dupes.Distinct();
        }

        // A rule's form count, probed over 0..200, which covers every branch of every rule
        // (including Arabic's mod-100 cases).
        private static int MaxFormCount(Func<int, int> rule)
        {
            int max = 0;
            for (int n = 0; n <= 200; n++) max = Math.Max(max, rule(n));
            return max + 1;
        }
    }
}
