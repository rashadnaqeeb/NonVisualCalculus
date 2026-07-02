namespace DiscoAccess.Module.World
{
    /// <summary>
    /// The keys of the world-category input actions. The module registers <c>InputAction</c>s under these
    /// (in <see cref="DiscoAccess.Core.Input.InputCategory.World"/>); the glide keys are polled as a held
    /// vector each frame, while the verbs fire the <see cref="WorldReader"/>'s handlers. The counterpart to
    /// <see cref="DiscoAccess.Core.UI.Nav.UiActions"/> for the isometric world.
    /// </summary>
    internal static class WorldActions
    {
        public const string MoveNorth = "world.move.north";
        public const string MoveSouth = "world.move.south";
        public const string MoveEast = "world.move.east";
        public const string MoveWest = "world.move.west";
        public const string Recenter = "world.recenter";
        public const string Interact = "world.interact";
        public const string Walk = "world.walk";
        public const string Stop = "world.stop";

        // The scanner (review cursor): cycle its selection by category or quick-nav group, and act on it.
        public const string ScanNext = "world.scan.next";
        public const string ScanPrev = "world.scan.prev";
        public const string ScanNextCategory = "world.scan.category.next";
        public const string ScanPrevCategory = "world.scan.category.prev";
        public const string ScanPeopleNext = "world.scan.people.next";
        public const string ScanPeoplePrev = "world.scan.people.prev";
        public const string ScanItemsNext = "world.scan.items.next";
        public const string ScanItemsPrev = "world.scan.items.prev";
        public const string ScanExitsNext = "world.scan.exits.next";
        public const string ScanExitsPrev = "world.scan.exits.prev";
        public const string ScanInteract = "world.scan.interact";

        // Information screens, pause, help.
        public const string OpenInventory = "world.inventory";
        public const string OpenCharacterSheet = "world.charsheet";
        public const string OpenJournal = "world.journal";
        public const string OpenThoughtCabinet = "world.thoughtcabinet";
        public const string Pause = "world.pause";
        public const string Help = "world.help";

        // Gameplay quick-actions.
        public const string HealEndurance = "world.heal.endurance";
        public const string HealVolition = "world.heal.volition";
        public const string LeftHandItem = "world.hand.left";
        public const string RightHandItem = "world.hand.right";
        public const string QuickSave = "world.quicksave";
        public const string QuickLoad = "world.quickload";
        public const string Language = "world.language";

        // Status readouts.
        public const string ReadTime = "world.read.time";
        public const string ReadMoney = "world.read.money";
        public const string ReadHealth = "world.read.health";
        public const string ReadLocation = "world.read.location";
    }
}
