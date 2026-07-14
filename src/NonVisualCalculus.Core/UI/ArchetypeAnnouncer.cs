using System.Collections.Generic;

namespace NonVisualCalculus.Core.UI
{
    /// <summary>
    /// Composes the spoken line for a focused character-creation archetype from its
    /// <see cref="ArchetypeState"/>. Order follows the house style: the archetype name first (the
    /// distinguishing word), then the mechanical detail a player chooses on (each attribute as
    /// "name value", then the signature skill), then the flavor description last, so a quick navigator
    /// hears the name and stats before the longer text is cut by the next focus. Every word here is the
    /// game's own localized text spoken verbatim; nothing is mod-authored.
    /// </summary>
    public static class ArchetypeAnnouncer
    {
        public static string Compose(ArchetypeState s)
        {
            var parts = new List<string?> { s.Name };
            foreach (ArchetypeAttribute a in s.Attributes)
                parts.Add(a.Name + " " + a.Value);
            parts.Add(s.SignatureSkill);
            parts.Add(s.Description);
            return Text.SpokenLine.Join(", ", parts);
        }
    }
}
