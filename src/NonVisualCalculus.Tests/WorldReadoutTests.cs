using NonVisualCalculus.Core.Strings;
using Xunit;

namespace NonVisualCalculus.Tests
{
    // The mod-authored world readouts composed in Core (money, health, the walk-then-interact feedback).
    // The bar and target names come from the game at runtime; here they stand in as fixed strings.
    public class WorldReadoutTests
    {
        [Fact]
        public void Money_CentimsToReal_TwoDecimals()
        {
            Assert.Equal("8.62 réal", Strings.WorldMoney(862));
            Assert.Equal("0.05 réal", Strings.WorldMoney(5));   // pads the centims
            Assert.Equal("12.00 réal", Strings.WorldMoney(1200));
            Assert.Equal("0.00 réal", Strings.WorldMoney(0));
        }

        [Fact]
        public void Health_NamesBarsAndCounts_Charges()
        {
            Assert.Equal(
                "Health 2/2, 0 healing charges; Morale 1/1, 2 healing charges",
                Strings.WorldHealth("Health", 2, 2, 0, "Morale", 1, 1, 2));
        }

        [Fact]
        public void HealCharges_SingularPlural()
        {
            Assert.Equal("1 healing charge", Strings.HealCharges(1));
            Assert.Equal("0 healing charges", Strings.HealCharges(0));
            Assert.Equal("3 healing charges", Strings.HealCharges(3));
        }

        [Fact]
        public void WalkFeedback_NameFirst_OrFallsBackWhenNameless()
        {
            Assert.Equal("moving to Cuno", Strings.WorldMovingTo("Cuno"));
            Assert.Equal("moving", Strings.WorldMovingTo(""));        // nameless target -> bare-move wording
            Assert.Equal("Cuno, can't reach", Strings.WorldUnreachable("Cuno"));
            Assert.Equal("can't reach", Strings.WorldUnreachable(""));
        }
    }
}
