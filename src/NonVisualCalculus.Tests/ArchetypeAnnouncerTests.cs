using NonVisualCalculus.Core.UI;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class ArchetypeAnnouncerTests
    {
        private static ArchetypeState Thinker() => new ArchetypeState(
            "Thinker",
            new[]
            {
                new ArchetypeAttribute("Intellect", 5),
                new ArchetypeAttribute("Psyche", 1),
                new ArchetypeAttribute("Physique", 2),
                new ArchetypeAttribute("Motorics", 4),
            },
            "+Encyclopedia",
            "Extremely intelligent. Very bad with people.");

        [Fact]
        public void Compose_OrdersNameAttributesSignatureThenDescription()
        {
            Assert.Equal(
                "Thinker, Intellect 5, Psyche 1, Physique 2, Motorics 4, +Encyclopedia, " +
                "Extremely intelligent. Very bad with people.",
                ArchetypeAnnouncer.Compose(Thinker()));
        }

        [Fact]
        public void Compose_NoAttributesOrSignature_ReadsNameAndDescription()
        {
            var state = new ArchetypeState(
                "ARCHETYPE",
                new ArchetypeAttribute[0],
                signatureSkill: null,
                description: "A description.");
            Assert.Equal("ARCHETYPE, A description.", ArchetypeAnnouncer.Compose(state));
        }

        [Fact]
        public void Compose_OmitsEmptyDescription()
        {
            var state = new ArchetypeState(
                "THINKER",
                new[] { new ArchetypeAttribute("Intellect", 5) },
                signatureSkill: null,
                description: null);
            Assert.Equal("THINKER, Intellect 5", ArchetypeAnnouncer.Compose(state));
        }
    }
}
