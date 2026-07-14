using NonVisualCalculus.Core.UI;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class InventoryItemAnnouncerTests
    {
        [Fact]
        public void NameAlone_ReadsName()
        {
            Assert.Equal("Tape Player",
                InventoryItemAnnouncer.Compose(new InventoryItemState { Name = "Tape Player" }));
        }

        [Fact]
        public void FreshItem_AppendsNew()
        {
            Assert.Equal("Bottle, new",
                InventoryItemAnnouncer.Compose(new InventoryItemState { Name = "Bottle", IsFresh = true }));
        }

        [Fact]
        public void Uses_SingularAndPlural()
        {
            Assert.Equal("Pilsner, 1 use",
                InventoryItemAnnouncer.Compose(new InventoryItemState { Name = "Pilsner", Uses = 1 }));
            Assert.Equal("Pilsner, 4 uses",
                InventoryItemAnnouncer.Compose(new InventoryItemState { Name = "Pilsner", Uses = 4 }));
        }

        [Fact]
        public void Value_SpokenOnlyWhenPositive_AsReal()
        {
            Assert.Equal("Mug, value 0.05 réal",
                InventoryItemAnnouncer.Compose(new InventoryItemState { Name = "Mug", Value = 5 }));
            Assert.Equal("Mug",
                InventoryItemAnnouncer.Compose(new InventoryItemState { Name = "Mug", Value = 0 }));
        }

        [Fact]
        public void PawnLabel_ReplacesTheValueMarker()
        {
            Assert.Equal("Shift Card, Pawn for 13.13 réal",
                InventoryItemAnnouncer.Compose(new InventoryItemState
                {
                    Name = "Shift Card",
                    Value = 1313,
                    PawnLabel = "Pawn for",
                }));
        }

        [Fact]
        public void EffectsAndDescription_FollowTheName()
        {
            Assert.Equal("Hat, +1 Encyclopedia: Book-smart, A wide-rimmed hat.",
                InventoryItemAnnouncer.Compose(new InventoryItemState
                {
                    Name = "Hat",
                    Effects = "+1 Encyclopedia: Book-smart",
                    Description = "A wide-rimmed hat.",
                }));
        }

        [Fact]
        public void FullOrder_NameFreshUsesValueEffectsDescription()
        {
            Assert.Equal("Sneakers, new, value 0.02 réal, +1 Reaction Speed, Fast shoes.",
                InventoryItemAnnouncer.Compose(new InventoryItemState
                {
                    Name = "Sneakers",
                    IsFresh = true,
                    Value = 2,
                    Effects = "+1 Reaction Speed",
                    Description = "Fast shoes.",
                }));
        }
    }
}
