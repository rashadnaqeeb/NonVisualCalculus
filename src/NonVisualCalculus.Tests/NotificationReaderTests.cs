using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.Text;
using Xunit;

namespace NonVisualCalculus.Tests
{
    // The Core composition behind the HUD-notification reader: joining a notification's title and detail,
    // and the existential-crisis heal prompt. The header/description and the bar name and game message come
    // from the game at runtime; here they stand in as fixed strings.
    public class NotificationReaderTests
    {
        [Fact]
        public void Compose_JoinsHeaderAndDescription()
        {
            Assert.Equal("MONEY, +5 réal", NotificationText.Compose("MONEY", "+5 réal"));
        }

        [Fact]
        public void Compose_OneSideEmpty_StandsAlone()
        {
            Assert.Equal("LEVEL UP", NotificationText.Compose("LEVEL UP", ""));
            Assert.Equal("LEVEL UP", NotificationText.Compose("LEVEL UP", null));
            Assert.Equal("a detail", NotificationText.Compose("", "a detail"));
            Assert.Equal("", NotificationText.Compose(null, null));
        }

        [Fact]
        public void Compose_RepeatedPart_ReadOnce()
        {
            Assert.Equal("ITEM", NotificationText.Compose("ITEM", "item"));
        }

        [Fact]
        public void Compose_CleansMarkup()
        {
            Assert.Equal("MONEY, +5 réal", NotificationText.Compose("<b>MONEY</b>", "+5 réal"));
        }

        [Fact]
        public void CrisisHeal_HealthHealsLeft_MoraleHealsRight()
        {
            Assert.Equal("Health critical, press left arrow to heal. Heal yourself!",
                Strings.CrisisHeal("Health", healWithLeft: true, "Heal yourself!"));
            Assert.Equal("Morale critical, press right arrow to heal. Heal yourself!",
                Strings.CrisisHeal("Morale", healWithLeft: false, "Heal yourself!"));
        }

        [Fact]
        public void CrisisHeal_NoGameMessage_PromptStandsAlone()
        {
            Assert.Equal("Health critical, press left arrow to heal",
                Strings.CrisisHeal("Health", healWithLeft: true, null));
        }
    }
}
