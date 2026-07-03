using DiscoAccess.Core.Text;
using Xunit;

namespace DiscoAccess.Tests
{
    /// <summary>
    /// The RTL unfix: display-shaped Arabic (presentation forms in visual order, as DE's fixForRTL
    /// produces for the renderer) comes back to logical order for the synthesizer. The fixed inputs
    /// are real strings captured from the live game running in Arabic.
    /// </summary>
    public class RtlTextTests
    {
        [Fact]
        public void Unfix_RestoresLogicalOrder_ForAFixedActorName()
        {
            // Kim Kitsuragi's localized name as DE's actor lookup returns it: presentation forms,
            // visually ordered ("Kitsuragi Kim" reversed glyph by glyph).
            Assert.Equal("كيم كيتسوراجي", RtlText.Unfix("ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ"));
        }

        [Fact]
        public void Unfix_KeepsAnEmbeddedLatinRunUpright()
        {
            // The motor skiff: the fixer leaves "A72" in logical order inside the visual string, so
            // the unfix must flip everything back except that run.
            Assert.Equal("قارب روو A72 بمحرك", RtlText.Unfix("كﺮﺤﻤﺑ A72 وور برﺎﻗ"));
        }

        [Fact]
        public void Unfix_LeavesLogicalArabicAlone()
        {
            // Plain base letters (what a translation file or keyboard produces) carry no presentation
            // forms, so the unfix must not touch them - double-unfixing would garble.
            Assert.Equal("كيم كيتسوراجي", RtlText.Unfix("كيم كيتسوراجي"));
        }

        [Fact]
        public void Unfix_LeavesLatinTextAlone()
        {
            Assert.Equal("Kim Kitsuragi, 3 meters", RtlText.Unfix("Kim Kitsuragi, 3 meters"));
        }

        [Fact]
        public void Unfix_InvertsOnlyTheFixedName_InAComposedScannerLine()
        {
            // The scanner composes a fixed game name with the mod's own logical text; only the name's
            // cluster inverts, in place. (The live bug this pins: a whole-line reversal spoke
            // "above ,meters 3 ,east ;<name>".)
            Assert.Equal("كيم كيتسوراجي; east, 3 meters, above",
                RtlText.Unfix("ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ; east, 3 meters, above"));
            Assert.Equal("moving to كيم كيتسوراجي",
                RtlText.Unfix("moving to ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ"));
        }

        [Fact]
        public void Unfix_InvertsSpeakerAndLineSeparately_AcrossTheColon()
        {
            // The dialogue reader composes "<speaker>: <line>", both fixed by the game: each cluster
            // inverts within its own position, so the speaker stays first.
            Assert.Equal("كيم: كيم كيتسوراجي", RtlText.Unfix("ﻢﻴﻛ: ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ"));
        }

        [Fact]
        public void Unfix_LeavesALogicalCluster_BesideAFixedOne()
        {
            // A translated mod word (logical, from ar.txt) composed after a fixed game name: only the
            // fixed cluster inverts; per-cluster gating keeps the logical one intact.
            Assert.Equal("كيم كيتسوراجي; شرق", RtlText.Unfix("ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ; شرق"));
        }

        [Fact]
        public void Unfix_BoundsClusters_AtLineBreaks()
        {
            // The game's fixer reverses per LINE, so each line of a multi-line fixed string is its own
            // unit; a cluster crossing the break would reverse both lines together and swap their order.
            Assert.Equal("كيم\nكيم كيتسوراجي", RtlText.Unfix("ﻢﻴﻛ\nﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ"));
        }

        [Fact]
        public void UnfixLogical_RestoresAPreReversedLatinRun()
        {
            // An RTL-flagged TMP label captured live (a thought name): logical-order Arabic, but the
            // Latin/digit run is stored pre-reversed to survive TMP's render reversal - including its
            // hyphen, and sitting outside any Arabic-anchored span, which is why the flag-aware call
            // exists instead of the vote.
            Assert.Equal("فيرويذر T-500", RtlText.UnfixLogical("ﻓﻴﺮﻭﻳﺬﺭ 005-T"));
            // A clock time reverses as one run, its colon a joiner ("people sleep at 21:00"): the
            // digit pairs must not restore separately, which would speak 00:21.
            Assert.Equal("الساعة 21:00", RtlText.UnfixLogical("اﻟﺴﺎﻋﺔ 00:12"));
        }

        [Fact]
        public void UnfixLogical_SwapsPreMirroredPairs()
        {
            // The same storage pre-mirrors paired punctuation (a live thought name with a caliber
            // parenthetical): the closing paren comes first in memory and must swap back.
            Assert.Equal("مسدسات الأصابع (عيار 9 مم)",
                RtlText.UnfixLogical("ﻣﺴﺪﺳﺎت اﻷﺻﺎﺑﻊ )ﻋﻴﺎر 9 ﻣﻢ("));
        }

        [Fact]
        public void Unfix_PullsAnEdgeDigitRun_BackToItsLogicalEnd()
        {
            // A fixed name ending in a number ("Door, Room #1" style) carries the digits at its visual
            // FRONT (the fixer keeps them upright); the reversal must include them so they return to
            // the logical end rather than stay stranded before the name.
            Assert.Equal("كيم كيتسوراجي 27", RtlText.Unfix("27 ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ"));
            // Digits TRAILING the Arabic are a separately composed segment (the game's own day-plus-
            // clock string, our floor readout) and must stay in place, outside the reversal.
            Assert.Equal("كيم كيتسوراجي 08:06", RtlText.Unfix("ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ 08:06"));
        }

        [Fact]
        public void Unfix_FoldsWithoutReversing_LogicalOrderShapedText()
        {
            // An RTL-flagged TMP label (captured from the live loading tip) carries presentation forms
            // in LOGICAL order - TMP itself reverses at render time - so only the shaping folds to base
            // letters; the visual-order reversal would garble it.
            Assert.Equal("يمكن تجربة",
                RtlText.Unfix("ﻳﻤﻜﻦ ﺗﺠﺮﺑﺔ"));
            // Its paired punctuation is stored pre-mirrored to survive TMP's render reversal (a
            // pronunciation tip brackets its words), so the fold swaps it back.
            Assert.Equal("يمكن [تجربة] يمكن",
                RtlText.Unfix("ﻳﻤﻜﻦ ]ﺗﺠﺮﺑﺔ[ ﻳﻤﻜﻦ"));
        }

        [Fact]
        public void SpokenLine_UnfixesEachPart_BeforeTheJoin()
        {
            // A fixed game caption composed with the mod's own logical role word (the focus line
            // "استمرار, زر"). After the join no character can mark the boundary - a whole-line unfix
            // reverses the logical word and flips the order - so each part inverts within itself.
            Assert.Equal("كيم كيتسوراجي, زر", SpokenLine.Join("ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ", "زر"));
        }

        [Fact]
        public void StringsTemplates_UnfixAFixedArgument_InsideItsSlot()
        {
            // A fixed game name flows into an authored template's {0}; it must invert within the slot,
            // before the template's own (possibly Arabic) words surround it.
            Assert.Equal("moving to كيم كيتسوراجي",
                DiscoAccess.Core.Strings.Strings.WorldMovingTo("ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ"));
        }

        [Fact]
        public void Clean_UnfixesFixedArabic_EndToEnd()
        {
            // The speech funnel: every reader's text passes TextFilter.Clean, so fixed Arabic from any
            // game surface (dialogue, names, tooltips) reaches the synthesizer logical.
            Assert.Equal("كيم كيتسوراجي", TextFilter.Clean("ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ"));
        }

        [Fact]
        public void Clean_StripsBidiControlCharacters()
        {
            // RLM + RLE ... PDF, LRM, and an isolate pair around RTL text: direction marks are visual
            // layout, meaningless to a synthesizer, and some announce them.
            const string rlm = "‏", rle = "‫", pdf = "‬", lrm = "‎";
            const string lri = "⁦", pdi = "⁩";
            string marked = rlm + rle + "مرحبا" + pdf + " " + lrm + "disco" + lri + pdi;
            Assert.Equal("مرحبا disco", TextFilter.Clean(marked));
        }

        [Fact]
        public void TypeAhead_MatchesLogicalTyping_AgainstAFixedLabel()
        {
            // Type-ahead: the user types logical Arabic; a list label read from the game may be
            // display-fixed. Typing "كيم" must land on the fixed "Kim Kitsuragi" label.
            var items = new[] { "Bottle", "ﻲﺟارﻮﺴﺘﻴﻛ ﻢﻴﻛ" };
            var search = new DiscoAccess.Core.UI.Nav.TypeAheadSearch();
            int landed = -1;
            foreach (char c in "كيم") search.AddChar(c);
            search.Search(items.Length, i => items[i], i => landed = i);
            Assert.Equal(1, landed);
        }
    }
}
