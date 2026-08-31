using System;
using System.Collections.Generic;
using System.Numerics;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// A built-in bookmark: a notable location the mod ships, listed in the bookmarks menu after the
    /// player's own bookmarks (toggleable via the show-preset-bookmarks setting) and walkable like any
    /// bookmark, but never stored in the player's bookmarks file and not deletable. The name is a
    /// provider resolving through the strings table at speak time so it follows the mod language - the
    /// reason presets stay out of the file, where a stored name could not retranslate.
    /// </summary>
    public sealed class PresetBookmark
    {
        public string Scene { get; }
        public Func<string> Name { get; }
        /// <summary>The spot in the mod's world frame (see Bookmark), on walkable ground with a
        /// connected path from open ground - a spot behind a closed door would always read "can't
        /// reach", so interiors are marked at their entrance instead.</summary>
        public Vector3 Position { get; }
        /// <summary>First in-game day the entry is listed (the game's 1-based day counter). The coast
        /// opens on day 3; until then its presets would only ever read as unreachable.</summary>
        public int MinDay { get; }

        public PresetBookmark(string scene, Func<string> name, Vector3 position, int minDay = 1)
        {
            Scene = scene;
            Name = name;
            Position = position;
            MinDay = minDay;
        }
    }

    /// <summary>
    /// The mod's built-in bookmarks. Positions adapted from the disco-accessibility mod's
    /// community-contributed waypoints (by BlindGuyNW, with permission), converted to the mod's world
    /// frame and re-verified against the live game's navmesh.
    /// </summary>
    public static class PresetBookmarks
    {
        // The one scene holding the whole playable map, coast included (the game's internal scene id).
        private const string Martinaise = "Martinaise-ext";

        public static readonly IReadOnlyList<PresetBookmark> All = new[]
        {
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetWhirling, new Vector3(20.025904f, 4.2775083f, 85.19878f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetHarbourOffice, new Vector3(43.941692f, 9.141584f, 101.35865f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetUnionOffice, new Vector3(47.5f, 11.3437f, 106.1f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetFrittte, new Vector3(42.338387f, 4.271736f, 94.79665f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetPawnshop, new Vector3(2.8713768f, 2.9387414f, 54.079056f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetBookstore, new Vector3(3.986334f, 4.341721f, 89.59491f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetJoyceBoat, new Vector3(-1.2979f, 3.6305f, 105.692f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetHarbourGate, new Vector3(61.099182f, 11.343679f, 98.2997f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetCrimeScene, new Vector3(19.139465f, 3.994326f, 113.23623f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetPierApartments, new Vector3(1.6209f, 3.2656f, 137.1991f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetApartmentCourtyard, new Vector3(22.68f, 3.8282f, 126.4114f)),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetFishingVillage, new Vector3(-67.36704f, 3.846819f, 68.67498f), minDay: 3),
            new PresetBookmark(Martinaise, () => Strings.Strings.PresetChurch, new Vector3(-68.47862f, 3.0798886f, 108.493286f), minDay: 3),
        };

        /// <summary>The presets listable on <paramref name="scene"/> on in-game day
        /// <paramref name="day"/>, table order.</summary>
        public static List<PresetBookmark> For(string scene, int day)
        {
            var list = new List<PresetBookmark>();
            foreach (PresetBookmark preset in All)
                if (preset.Scene == scene && day >= preset.MinDay)
                    list.Add(preset);
            return list;
        }
    }
}
