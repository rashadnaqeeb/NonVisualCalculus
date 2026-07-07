using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// The bookmarks file format: one bookmark per line, <c>scene|name|x|y|z</c>, with blank lines and
    /// '#' comments ignored. Plain text so a hand edit is possible, though bookmarks are normally added
    /// in game (the coordinates are the mod's world frame, not Unity's - see <see cref="Bookmark"/>).
    /// Parsing warns and skips a malformed line rather than dropping the whole file: losing one line to
    /// a stray edit must not silently lose every bookmark.
    /// </summary>
    public static class BookmarkFile
    {
        private const char Separator = '|';

        public static List<Bookmark> Parse(string? text, Action<string> warn)
        {
            var list = new List<Bookmark>();
            if (string.IsNullOrEmpty(text)) return list;
            string[] lines = text!.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r').Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split(Separator);
                if (parts.Length != 5
                    || !TryFloat(parts[2], out float x)
                    || !TryFloat(parts[3], out float y)
                    || !TryFloat(parts[4], out float z))
                {
                    warn($"bookmarks line {i + 1} is not 'scene|name|x|y|z'; skipped: '{line}'");
                    continue;
                }
                string scene = parts[0].Trim();
                string name = parts[1].Trim();
                if (scene.Length == 0 || name.Length == 0)
                {
                    warn($"bookmarks line {i + 1} has an empty scene or name; skipped: '{line}'");
                    continue;
                }
                list.Add(new Bookmark(scene, name, new Vector3(x, y, z)));
            }
            return list;
        }

        public static string Serialize(IEnumerable<Bookmark> bookmarks)
        {
            var sb = new StringBuilder();
            sb.Append("# DiscoAccess bookmarks - one per line: scene|name|x|y|z\n");
            sb.Append("# Coordinates are the mod's world frame; add bookmarks in game rather than by hand.\n");
            foreach (Bookmark b in bookmarks)
            {
                sb.Append(Clean(b.Scene)).Append(Separator).Append(Clean(b.Name)).Append(Separator)
                  .Append(Num(b.Position.X)).Append(Separator)
                  .Append(Num(b.Position.Y)).Append(Separator)
                  .Append(Num(b.Position.Z)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>A name as typed, made safe for one file line: the separator and any control
        /// character become spaces, runs of whitespace collapse, ends trim. Empty in, empty out (the
        /// caller substitutes its default name).</summary>
        public static string Clean(string? name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(name!.Length);
            bool pendingSpace = false;
            foreach (char c in name)
            {
                if (c == Separator || char.IsControl(c) || char.IsWhiteSpace(c))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }
                if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool TryFloat(string s, out float value)
            => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static string Num(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
