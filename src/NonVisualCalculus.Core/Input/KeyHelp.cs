using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.Strings;

namespace NonVisualCalculus.Core.Input
{
    /// <summary>One action the key-help screen lists: its registry key (for group matching), its spoken
    /// label, and the display names of its live keyboard bindings. Produced by
    /// <see cref="InputManager.SnapshotLiveKeys"/>.</summary>
    public sealed class KeyHelpEntry
    {
        public string ActionKey { get; }
        public string Label { get; }
        public IReadOnlyList<string> Chords { get; }

        public KeyHelpEntry(string actionKey, string label, IReadOnlyList<string> chords)
        {
            ActionKey = actionKey;
            Label = label;
            Chords = chords;
        }
    }

    /// <summary>A set of actions the key-help screen reads as ONE line (the four navigation arrows, a
    /// next/previous pair like Home/End) under an authored label - a spoken list of near-identical lines
    /// is noise. The keys phrase is authored when the members' key names don't say it best ("arrow keys",
    /// "W A S D"), else null to derive it from the members' live bindings ("Home, End"), which stays true
    /// through a rebind. The collapse only applies when every member is live: with any member's key
    /// claimed elsewhere (the heal arrows shadowing UI Left/Right in a dialogue), the group line would
    /// promise keys that do something else, so the live members read individually instead.</summary>
    public sealed class KeyHelpGroup
    {
        public string Label { get; }
        public string? SpokenKeys { get; }
        public IReadOnlyList<string> Members { get; }

        public KeyHelpGroup(string label, string? spokenKeys, IReadOnlyList<string> members)
        {
            Label = label;
            SpokenKeys = spokenKeys;
            Members = members;
        }
    }

    /// <summary>
    /// Composes the key-help screen's spoken lines from a live-keys snapshot: one line per reachable
    /// action ("Read time, T"), complete groups collapsed to one line ("Navigate, arrow keys"), and each
    /// chord rendered in speakable, translatable words ("Ctrl+PageDown" as "Control Page down").
    /// </summary>
    public static class KeyHelp
    {
        public static List<string> Compose(IReadOnlyList<KeyHelpEntry> entries, IReadOnlyList<KeyHelpGroup> groups)
        {
            var present = new HashSet<string>();
            foreach (var e in entries) present.Add(e.ActionKey);

            // Only a COMPLETE group collapses (see KeyHelpGroup); map each member key to its group.
            var groupOf = new Dictionary<string, KeyHelpGroup>();
            foreach (var g in groups)
            {
                bool complete = true;
                foreach (var m in g.Members)
                    if (!present.Contains(m)) { complete = false; break; }
                if (!complete) continue;
                foreach (var m in g.Members) groupOf[m] = g;
            }

            var lines = new List<string>();
            var emitted = new HashSet<KeyHelpGroup>();
            foreach (var e in entries)
            {
                if (groupOf.TryGetValue(e.ActionKey, out var g))
                {
                    // The group line sits where its first member would have; later members add nothing.
                    if (emitted.Add(g))
                        lines.Add(Strings.Strings.KeyHelpLine(g.Label, g.SpokenKeys ?? DerivedKeys(g, entries)));
                    continue;
                }
                lines.Add(Strings.Strings.KeyHelpLine(e.Label, SpokenChords(e.Chords)));
            }
            return lines;
        }

        // A group's keys phrase read off its members' live bindings, in the members' listed (registration)
        // order, so a next/previous pair speaks next's key first ("Page down, Page up").
        private static string DerivedKeys(KeyHelpGroup g, IReadOnlyList<KeyHelpEntry> entries)
        {
            var chords = new List<string>();
            foreach (var e in entries)
            {
                bool member = false;
                foreach (var m in g.Members)
                    if (m == e.ActionKey) { member = true; break; }
                if (member) chords.AddRange(e.Chords);
            }
            return SpokenChords(chords);
        }

        private static string SpokenChords(IReadOnlyList<string> chords)
        {
            var parts = new string[chords.Count];
            for (int i = 0; i < chords.Count; i++) parts[i] = SpokenChord(chords[i]);
            return string.Join(", ", parts);
        }

        /// <summary>A binding display name ("Ctrl+PageDown") as speakable words ("Control Page down"):
        /// each '+'-separated token through the authored key names, unknown tokens (letters, F-keys)
        /// spoken as they are.</summary>
        public static string SpokenChord(string displayName)
        {
            var tokens = displayName.Split('+');
            for (int i = 0; i < tokens.Length; i++) tokens[i] = SpokenToken(tokens[i]);
            return string.Join(" ", tokens);
        }

        // Delegates, not resolved strings, so a language switch reads through to the live table.
        private static readonly Dictionary<string, Func<string>> TokenNames = new Dictionary<string, Func<string>>
        {
            ["Ctrl"] = () => Strings.Strings.KeyControl,
            ["Shift"] = () => Strings.Strings.KeyShift,
            ["Alt"] = () => Strings.Strings.KeyAlt,
            ["UpArrow"] = () => Strings.Strings.KeyUpArrow,
            ["DownArrow"] = () => Strings.Strings.KeyDownArrow,
            ["LeftArrow"] = () => Strings.Strings.KeyLeftArrow,
            ["RightArrow"] = () => Strings.Strings.KeyRightArrow,
            ["Return"] = () => Strings.Strings.KeyReturn,
            ["KeypadEnter"] = () => Strings.Strings.KeyKeypadEnter,
            ["Escape"] = () => Strings.Strings.KeyEscape,
            ["Backspace"] = () => Strings.Strings.KeyBackspace,
            ["Tab"] = () => Strings.Strings.KeyTab,
            ["Home"] = () => Strings.Strings.KeyHome,
            ["End"] = () => Strings.Strings.KeyEnd,
            ["Space"] = () => Strings.Strings.KeySpace,
            ["Comma"] = () => Strings.Strings.KeyComma,
            ["Period"] = () => Strings.Strings.KeyPeriod,
            ["Slash"] = () => Strings.Strings.KeySlash,
            ["PageUp"] = () => Strings.Strings.KeyPageUp,
            ["PageDown"] = () => Strings.Strings.KeyPageDown,
        };

        private static string SpokenToken(string token)
        {
            if (TokenNames.TryGetValue(token, out var name)) return name();
            // The digit row's KeyCodes ("Alpha1") speak as the bare digit.
            if (token.Length == 6 && token.StartsWith("Alpha", StringComparison.Ordinal) && char.IsDigit(token[5]))
                return token.Substring(5);
            return token;
        }
    }
}
