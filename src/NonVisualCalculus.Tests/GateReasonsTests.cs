using NonVisualCalculus.Core.World;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class GateReasonsTests
    {
        [Fact]
        public void For_FlashlightBoolean_NamesTheFlashlight()
        {
            Assert.Equal("requires a flashlight", GateReasons.For(GateReasons.WieldingFlashlight));
        }

        [Fact]
        public void For_UnknownBoolean_HasNoReason()
        {
            Assert.Null(GateReasons.For("auto.fortress_floor"));
        }
    }
}
