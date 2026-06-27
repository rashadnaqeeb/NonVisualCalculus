using System.Collections.Generic;

namespace DiscoAccess.Core.UI
{
    /// <summary>One of an archetype's four attributes: the localized attribute name and its value.</summary>
    public sealed class ArchetypeAttribute
    {
        public string Name { get; }
        public int Value { get; }

        public ArchetypeAttribute(string name, int value)
        {
            Name = name;
            Value = value;
        }
    }

    /// <summary>
    /// Unity-free snapshot of a focused character-creation archetype button (Thinker, Sensitive,
    /// Physical, or Create Your Own), extracted by the module adapter and composed into speech by
    /// <see cref="ArchetypeAnnouncer"/>. All text is the game's own localized strings; the attribute
    /// values come from each ability's flip-clock target (the settled number, read past its animation).
    /// The custom-character button carries no attributes and no signature skill, so both are optional.
    /// </summary>
    public sealed class ArchetypeState
    {
        public string Name { get; }
        public IReadOnlyList<ArchetypeAttribute> Attributes { get; }
        public string? SignatureSkill { get; }
        public string? Description { get; }

        public ArchetypeState(string name, IReadOnlyList<ArchetypeAttribute> attributes,
            string? signatureSkill, string? description)
        {
            Name = name;
            Attributes = attributes;
            SignatureSkill = signatureSkill;
            Description = description;
        }
    }
}
