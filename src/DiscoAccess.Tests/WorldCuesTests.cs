using DiscoAccess.Core.Audio;
using DiscoAccess.Core.World;
using Xunit;

namespace DiscoAccess.Tests
{
    public class WorldCuesTests
    {
        [Fact]
        public void EveryCategory_SoundsItsCue()
        {
            Assert.Equal(AudioCue.ThingNpc, WorldCues.CueFor(WorldTaxonomy.Npc));
            Assert.Equal(AudioCue.ThingInteractable, WorldCues.CueFor(WorldTaxonomy.Interactable));
            Assert.Equal(AudioCue.ThingContainer, WorldCues.CueFor(WorldTaxonomy.Container));
            Assert.Equal(AudioCue.ThingOrb, WorldCues.CueFor(WorldTaxonomy.Orb));
            // A door and a destination exit share the door sound: both are a way through.
            Assert.Equal(AudioCue.ThingDoor, WorldCues.CueFor(WorldTaxonomy.Door));
            Assert.Equal(AudioCue.ThingDoor, WorldCues.CueFor(WorldTaxonomy.Exit));
        }

        [Fact]
        public void UnknownCategory_FallsBackToInteractable_NeverSilent()
        {
            Assert.Equal(AudioCue.ThingInteractable, WorldCues.CueFor("something-new"));
        }

        [Fact]
        public void OpenDoor_SoundsItsOwnCue_ClosedTheDefault()
        {
            Assert.Equal(AudioCue.ThingDoorOpen, WorldCues.CueFor(WorldTaxonomy.Door, isOpen: true));
            Assert.Equal(AudioCue.ThingDoor, WorldCues.CueFor(WorldTaxonomy.Door, isOpen: false));
            Assert.Equal(AudioCue.ThingDoor, WorldCues.CueFor(WorldTaxonomy.Exit, isOpen: false));
        }
    }
}
