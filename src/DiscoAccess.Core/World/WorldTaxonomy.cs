using System.Collections.Generic;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// The classification of world things the sensing layer reads. Two granularities: the fine
    /// classification (<see cref="All"/>, what a proxy reports as its <c>Category</c> and the naming rules
    /// key off - a plain in-place door names differently from a destination exit), and the browse categories
    /// (<see cref="Scan"/>, what the scanner cycles and the sonar's "what to sonify" toggles list), where a
    /// door lists under exit: to the player a door is a way through, whether it swings open in place or
    /// changes area (the WOTR rule - a closed door cuts the navmesh, so the door IS the exit there).
    /// <see cref="ScanCategory"/> maps the first onto the second. The keys are stable settings-path
    /// segments, never spoken; the menu and scanner map them to authored display names.
    /// </summary>
    public static class WorldTaxonomy
    {
        public const string Npc = "npc";
        public const string Door = "door";
        public const string Exit = "exit";
        public const string Container = "container";
        public const string Orb = "orb";
        public const string Interactable = "interactable";

        /// <summary>Every fine classification a proxy can report, in readout order.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Npc, Door, Exit, Container, Orb, Interactable };

        /// <summary>The browse categories the scanner cycles (and the sonar lists), in cycle order.</summary>
        public static readonly IReadOnlyList<string> Scan = new[] { Npc, Interactable, Container, Orb, Exit };

        /// <summary>The browse category a thing lists under: its own classification, except a door, which
        /// lists as an exit.</summary>
        public static string ScanCategory(string category) => category == Door ? Exit : category;
    }
}
