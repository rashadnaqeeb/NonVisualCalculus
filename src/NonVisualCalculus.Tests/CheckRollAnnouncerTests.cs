using System.Collections.Generic;
using NonVisualCalculus.Core.UI;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class CheckRollAnnouncerTests
    {
        private static CheckRollState State(int d1, int d2, int skill, string name, int baseTarget,
            params (string Name, int Bonus)[] mods)
        {
            var list = new List<CheckRollModifier>();
            foreach (var m in mods)
                list.Add(new CheckRollModifier(m.Name, m.Bonus));
            return new CheckRollState(d1, d2, skill, name, baseTarget, list);
        }

        // A target-raising modifier (bonus +1) is a hindrance, so it reads "minus 1"; folding it onto the
        // roll side lets 2 + 4 + 5 - 1 add up to the headline 10 against the base target 10.
        [Fact]
        public void Compose_HindranceModifier_ReadsMinus_AndFoldsIntoTotal()
        {
            var s = State(2, 4, 5, "Interfacing", 10, ("Lonesome Long Way Home", 1));
            Assert.Equal("10/10: rolled 2 plus 4, plus 5 Interfacing, minus 1 Lonesome Long Way Home",
                CheckRollAnnouncer.Compose(s));
        }

        // A target-lowering modifier (bonus -2) is a help, so it reads "plus 2": 5 + 4 + 5 - (-2) = 16.
        [Fact]
        public void Compose_HelpModifier_ReadsPlus()
        {
            var s = State(5, 4, 5, "Interfacing", 10, ("Kind Eyes", -2));
            Assert.Equal("16/10: rolled 5 plus 4, plus 5 Interfacing, plus 2 Kind Eyes",
                CheckRollAnnouncer.Compose(s));
        }

        [Fact]
        public void Compose_NoModifiers_EndsAfterSkill()
        {
            var s = State(6, 2, 5, "Interfacing", 10);
            Assert.Equal("13/10: rolled 6 plus 2, plus 5 Interfacing", CheckRollAnnouncer.Compose(s));
        }

        // Several modifiers each fold in with their own sign; the total nets them all: 3 + 3 + 4 - (1 - 2).
        [Fact]
        public void Compose_MultipleModifiers_NetIntoTotal()
        {
            var s = State(3, 3, 4, "Logic", 12, ("Drunk", 1), ("Hunch", -2));
            Assert.Equal("11/12: rolled 3 plus 3, plus 4 Logic, minus 1 Drunk, plus 2 Hunch",
                CheckRollAnnouncer.Compose(s));
        }

        // The critical word (game text, passed through) ends the line: a double six succeeds even when the
        // total misses the target, so without it 14/15 followed by the game's Success reads as a
        // contradiction.
        [Fact]
        public void Compose_Critical_AppendsGameWord()
        {
            var s = new CheckRollState(6, 6, 2, "Savoir Faire", 15, new List<CheckRollModifier>(),
                "Critical success");
            Assert.Equal("14/15: rolled 6 plus 6, plus 2 Savoir Faire, Critical success",
                CheckRollAnnouncer.Compose(s));
        }

        // A passive check reads just the bare headline: no dice, the flat passive base of 6 folded into
        // the total the way the game's tooltip displays it (skill 5 shows as roll 11).
        [Fact]
        public void Compose_Passive_ReadsBareTotalOverTarget()
        {
            var s = new CheckRollState(0, 0, 5, "Volition", 10, new List<CheckRollModifier>(),
                critical: null, passive: true);
            Assert.Equal("11/10", CheckRollAnnouncer.Compose(s));
        }

        // A passive check's modifiers still net into its total (7 + 6 - 1), even though the line stays bare.
        [Fact]
        public void Compose_PassiveWithModifier_NetsIntoTotal()
        {
            var s = new CheckRollState(0, 0, 7, "Empathy", 13,
                new List<CheckRollModifier> { new CheckRollModifier("Drunk", 1) },
                critical: null, passive: true);
            Assert.Equal("12/13", CheckRollAnnouncer.Compose(s));
        }
    }
}
