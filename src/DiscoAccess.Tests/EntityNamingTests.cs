using System.Collections.Generic;
using DiscoAccess.Core.World;
using Xunit;

namespace DiscoAccess.Tests
{
    public class EntityNamingTests
    {
        private static string Resolve(string? name, string? authored = null, string? title = null, bool named = false,
                                      string cat = WorldTaxonomy.Container, IReadOnlyCollection<string>? area = null)
            => EntityNaming.Resolve(name, authored, title, named, cat, area);

        [Fact]
        public void AuthoredConversant_IsPreferredForProps()
        {
            // The game's own examine name beats the noun extractor: "Pile of Eternite", not "eternite".
            Assert.Equal("Pile of Eternite",
                Resolve("Eternite_door", authored: "Pile of Eternite", title: "YARD / PILE OF ETERNITE", cat: WorldTaxonomy.Interactable));
            Assert.Equal("Drainage Pipe", Resolve("Drainage Pipe", authored: "Drainage Pipe", cat: WorldTaxonomy.Interactable));
        }

        [Fact]
        public void NamedCharacter_TitleActorName_KeepsNameBeforeComma()
        {
            // "Name, the Title" actor names read just the name: "Garte", not the discarded lowercase fallback.
            Assert.Equal("Garte", Resolve("garte", authored: "Garte, the Cafeteria Manager", named: true, cat: WorldTaxonomy.Npc));
            Assert.Equal("Lilienne", Resolve("npc_lilienne", authored: "Lilienne, the Net Picker", named: true, cat: WorldTaxonomy.Npc));
        }

        [Fact]
        public void NamedCharacter_PrefersAuthoredName_DroppingLocationPrefix()
        {
            // "Yard Cuno" is the GameObject name; the authored actor name "Cuno" is what the game shows.
            Assert.Equal("Cuno", Resolve("Yard Cuno", authored: "Cuno", named: true, cat: WorldTaxonomy.Npc));
            // Even a slug GameObject name yields the real name when the game authored one.
            Assert.Equal("Cuno", Resolve("npc_cunoesse", authored: "Cuno", named: true, cat: WorldTaxonomy.Npc));
        }

        [Fact]
        public void NameMatchesActor_WholeNameModuloCaseAndSeparators()
        {
            // The plaza pair shares one conversation: the GameObject "fuckTheWorld" IS the actor
            // "Fuck the World", while "pissflaubert" is not the censored actor name "Pissf****t".
            Assert.True(EntityNaming.NameMatchesActor("fuckTheWorld", "Fuck the World"));
            Assert.False(EntityNaming.NameMatchesActor("pissflaubert", "Pissf****t"));
            // A location-prefixed name is not an identity, and empty never matches anything.
            Assert.False(EntityNaming.NameMatchesActor("Yard Cuno", "Cuno"));
            Assert.False(EntityNaming.NameMatchesActor("", ""));
        }

        [Fact]
        public void Exit_NamedByDestination_WithPortalType_HyphensBecomeSpaces()
        {
            // An exit reads the place it leads to (localized, hyphens spoken as spaces) plus the portal type
            // read from the GameObject.name: "Whirling-in-Rags" through a "...-door-..." reads "Whirling in
            // Rags door".
            Assert.Equal("Whirling in Rags door",
                Resolve("waterfront-door-rooftop", authored: "Whirling-in-Rags", cat: WorldTaxonomy.Exit));
            Assert.Equal("Cuno's shack door",
                Resolve("courtyard-door-cunos-shack", authored: "Cuno's shack", cat: WorldTaxonomy.Exit));
            // No portal word in the name falls back to the generic "exit": the tent flap's name is just "tent".
            Assert.Equal("Tent exit", Resolve("tent", authored: "Tent", cat: WorldTaxonomy.Exit));
            // A harbour gate reads "gate"; an inter-floor staircase reads "stairs".
            Assert.Equal("Docks gate", Resolve("harbor-gate-1", authored: "Docks", cat: WorldTaxonomy.Exit));
            Assert.Equal("Whirling in Rags stairs",
                Resolve("whirling-stairs-f2", authored: "Whirling-in-Rags", cat: WorldTaxonomy.Exit));
        }

        [Fact]
        public void ExitDestination_FloorWhenNamesCollide_ElseTheDistinctName()
        {
            // All Whirling floors share "Whirling-in-Rags", so the shared name says nothing: use the floor.
            Assert.Equal("floor 2",
                EntityNaming.ExitDestinationLabel("Whirling-int-f2", "Whirling-in-Rags", "Whirling-in-Rags"));
            Assert.Equal("floor 3",
                EntityNaming.ExitDestinationLabel("Whirling-int-f3-antechamber", "Whirling-in-Rags", "Whirling-in-Rags"));
            // The Doomed basement (-s1) shares its name with the floor above, so it reads "basement".
            Assert.Equal("basement",
                EntityNaming.ExitDestinationLabel("Doomed-commerce-int-s1", "Doomed Commercial Area", "Doomed Commercial Area"));
            // A floor the game names distinctly is preferred over a floor number: "Bookstore", not "floor 1".
            Assert.Equal("Bookstore",
                EntityNaming.ExitDestinationLabel("Doomed-commerce-int-f1", "Bookstore", "Doomed Commercial Area"));
            // A different building is distinct, so its own name is used (hyphens spaced later, in Resolve).
            Assert.Equal("Whirling-in-Rags",
                EntityNaming.ExitDestinationLabel("Whirling-int-f1", "Whirling-in-Rags", "Martinaise"));
            // The secretary's office is authored: the game labels that interior "Harbour", the same
            // word as the harbour itself, which would hide which door enters the office.
            Assert.Equal("secretary's office",
                EntityNaming.ExitDestinationLabel("Secretary-int", "Harbour", "Martinaise"));
            // The union boss's office and the yard's cargo container share that "Harbour" label
            // (the only three areas that carry it), so they are authored too.
            Assert.Equal("union office",
                EntityNaming.ExitDestinationLabel("Union-boss-int", "Harbour", "Martinaise"));
            Assert.Equal("cargo container",
                EntityNaming.ExitDestinationLabel("Union-container-int", "Harbour", "Martinaise"));
        }

        [Fact]
        public void InterFloorExit_ComposesWithType()
        {
            // The proxy passes the floor label as the authored name; the exit branch appends the portal type.
            Assert.Equal("floor 2 stairs",
                Resolve("Whirling 1st stairs", authored: "floor 2", cat: WorldTaxonomy.Exit));
        }

        [Fact]
        public void SpotDoor_NamedForTheSpot_DefaultsToDoor()
        {
            // An exterior door named for a specific spot (the proxy passes no destination so the coarse
            // "Martinaise" does not hide the spot): the spot leads, defaulting to "door".
            Assert.Equal("balcony door", Resolve("Balcony", cat: WorldTaxonomy.Exit));
            Assert.Equal("roof door", Resolve("Roof", cat: WorldTaxonomy.Exit));
        }

        [Fact]
        public void Exit_Elevator_ReadsElevator()
        {
            Assert.Equal("floor 3 elevator",
                Resolve("whirl1-elevator-whirl3", authored: "floor 3", cat: WorldTaxonomy.Exit));
        }

        [Fact]
        public void Exit_NoDestination_FallsBackToTypeWord()
        {
            Assert.Equal("door", Resolve("some-door-slug", cat: WorldTaxonomy.Exit)); // slug with a portal word
            Assert.Equal("exit", Resolve("trigger_zone", cat: WorldTaxonomy.Exit));  // slug, no portal word
        }

        [Fact]
        public void SpotFromDoorName_OnlyCleanNonTypePlaces()
        {
            Assert.Equal("balcony", EntityNaming.SpotFromDoorName("Balcony"));
            Assert.Null(EntityNaming.SpotFromDoorName("exit-courtyard"));   // a slug id
            Assert.Null(EntityNaming.SpotFromDoorName("Whirling Door"));    // contains a portal type
            Assert.Null(EntityNaming.SpotFromDoorName("Stairs"));           // is a portal type
        }

        [Fact]
        public void Door_PrefersAuthoredExamineHeader()
        {
            // A door with an examine actor speaks it: "Door, Room #1", not the dev name "Whirling Door
            // Tequila" (which also leaks a cast name).
            Assert.Equal("Door, Room #1",
                Resolve("Whirling Door Tequila", authored: "Door, Room #1", cat: WorldTaxonomy.Door));
            Assert.Equal("Blue door",
                Resolve("int-door-whirling-kitchen-backdoor", authored: "Blue door", cat: WorldTaxonomy.Door));
        }

        [Fact]
        public void CommaQualifier_IsKeptWhole_AppositiveIsNot()
        {
            // "Door, Room #1" qualifies after the comma - cutting it leaves a generic "Door" - while
            // "Name, the Title" is an appositive whose title is dropped.
            Assert.Equal("Door, Room #1",
                Resolve("whatever", authored: "Door, Room #1", cat: WorldTaxonomy.Interactable));
            Assert.Equal("Garte",
                Resolve("garte", authored: "Garte, the Cafeteria Manager", named: true, cat: WorldTaxonomy.Npc));
        }

        [Fact]
        public void Door_AuthoredFallback_NamesTheDevNamedDoors()
        {
            // The Whirling floor-2 bathroom doors have no conversation and raw dev names that leak cast
            // names; the mod's fallback table names them from the Strings set. Kitsuragi's joins the shared
            // bathroom to his room, so it reads as the connection rather than a second "bathroom door".
            Assert.Equal("connecting door",
                Resolve("Whirling Door Bathroom Kitsuragi", cat: WorldTaxonomy.Door));
            Assert.Equal("bathroom door",
                Resolve("Whirling Door Bathroom Klaasje", cat: WorldTaxonomy.Door));
            Assert.Equal("bathroom door",
                Resolve("Door Apartment no dialogue(bathroom)", cat: WorldTaxonomy.Door));
            // A locked no-destination exit-door shares the table (the Capeside scenery doors).
            Assert.Equal("locked door", Resolve("Locked-door_capeside-1", cat: WorldTaxonomy.Exit));
            // An authored examine header still beats the table.
            Assert.Equal("Door, Room #1",
                Resolve("Whirling Door Bathroom Kitsuragi", authored: "Door, Room #1", cat: WorldTaxonomy.Door));
        }

        [Fact]
        public void Exit_LadderAndBoat_ReadTheirPortalType()
        {
            Assert.Equal("ladder", Resolve("boardwalk-ladder-roof", cat: WorldTaxonomy.Exit));
            Assert.Equal("Sea Fortress boat",
                Resolve("fishing-boat", authored: "Sea Fortress", cat: WorldTaxonomy.Exit));
        }

        [Fact]
        public void UnusableAuthored_IsRejected_FallingBackToTheNoun()
        {
            Assert.Equal("crate", Resolve("Harbor Crate 22", authored: "You"));        // the player
            Assert.Equal("box", Resolve("box_3 rooftop", authored: "actor_5"));         // a machine id
            Assert.Equal("crate", Resolve("Harbor Crate 22", authored: "   "));         // blank
            Assert.Equal("stone", Resolve("stone_x", authored: "STONE PERC", cat: WorldTaxonomy.Interactable)); // meta token
        }

        [Fact]
        public void Container_GenericTypeWord_NamesByType_DroppingLocation()
        {
            // A generic container word names the thing by its type; the location decoration is dropped.
            Assert.Equal("crate", Resolve("Harbor Crate 22"));
            Assert.Equal("crate", Resolve("Fishmarket Crate"));
            Assert.Equal("bucket", Resolve("Yard Bucket"));
            Assert.Equal("money", Resolve("Church Bench Money"));
            Assert.Equal("metalbox", Resolve("Waterlock Metalbox"));
        }

        [Fact]
        public void Container_TypeWord_IsPositionIndependent()
        {
            // The type word is taken wherever it sits, so a name in "<type> <location>" order (the swap that
            // fooled last-word extraction) resolves the same as "<location> <type>".
            Assert.Equal("box", Resolve("Box Backroom"));      // type first, location last
            Assert.Equal("money", Resolve("FV money Shack"));  // location, type, then another location word
            Assert.Equal("box", Resolve("Box_Tare pier"));
        }

        [Fact]
        public void Container_TwoWordType_BeatsTheSingleWordInside()
        {
            Assert.Equal("trash can", Resolve("Pier Trash Can"));
            Assert.Equal("can", Resolve("can"));
        }

        [Fact]
        public void Container_StripsDuplicateSuffixesBeforeExtracting()
        {
            Assert.Equal("crate", Resolve("Crate (2)"));
            Assert.Equal("money", Resolve("Harbor Wall Money 1 (2)"));
            Assert.Equal("can", Resolve("Can (Clone)"));
        }

        [Fact]
        public void Container_SlugAndSpacedNames_ReduceAlike()
        {
            // Any separator tokenizes the same, so a slug and a spaced name yield the same type word.
            Assert.Equal("box", Resolve("box_3 rooftop"));
            Assert.Equal("crate", Resolve("crate_1 gate"));
            Assert.Equal("crate", Resolve("crate_landsend"));
            Assert.Equal("bottle", Resolve("empty bottle"));
            Assert.Equal("bottle", Resolve("empty_bottle (24)")); // adjective_noun: the noun still wins
        }

        [Fact]
        public void Container_SpecificItem_KeepsItsFullFlavorName()
        {
            // No generic type word, so the container is a specific item: speak its whole cleaned name rather
            // than a single extracted noun.
            Assert.Equal("leopard suit", Resolve("Leopard Suit"));
            Assert.Equal("filthy jacket", Resolve("Filthy-jacket", title: "BOARDWALK / RAILING")); // title ignored
        }

        [Fact]
        public void Container_TrailingContainerTag_IsDropped_KeepingFlavor()
        {
            Assert.Equal("phasmid nest", Resolve("Phasmid Nest Container"));
            Assert.Equal("flashlight", Resolve("Flashlight container"));
            Assert.Equal("police motor carriage", Resolve("police-motor-carriage container"));
        }

        [Fact]
        public void Container_PluralTypeWord_Matches_SpokenPlural()
        {
            // A regular plural still counts as the type; the plural form is spoken back.
            Assert.Equal("crates", Resolve("Jam Crates Right"));
            Assert.Equal("boxes", Resolve("Harbor Boxes"));
            Assert.Equal("barrels", Resolve("Fishmarket Barrels"));
        }

        [Fact]
        public void Container_LeadingLocation_IsStripped_FromAFlavorName()
        {
            // A compass word leads a flavor name off; the current area's stem strips a slug prefix.
            Assert.Equal("shack", Resolve("South Shack"));
            Assert.Equal("photo of rene",
                Resolve("martinaise-east-photo-of-rene", area: new[] { "martinaise" }));
        }

        [Fact]
        public void Container_LeadingDistrictSlug_IsStripped_LeavingTheNoun()
        {
            // A specific item whose noun is not a generic type still loses its leading district prefix.
            Assert.Equal("woodpile", Resolve("Yard Woodpile"));
            Assert.Equal("rock", Resolve("Landsend Rock"));
            Assert.Equal("machine", Resolve("Fishmarket Machine"));
            Assert.Equal("pillars", Resolve("Ice Pillars 3"));
            Assert.Equal("pile of clothes", Resolve("Village Pile Of Clothes"));
            Assert.Equal("cigars", Resolve("Jam Cigars")); // "jam" is the Traffic Jam district
        }

        [Fact]
        public void Container_DistrictStrip_LeavesRealFlavorNamesIntact()
        {
            // The leading word here is flavor, not a district, so the whole name survives.
            Assert.Equal("leopard suit", Resolve("Leopard Suit"));
            Assert.Equal("abandoned building pilsner", Resolve("Abandoned Building Pilsner"));
        }

        [Fact]
        public void Container_BuoyaToken_ReadsAsBuoyA()
        {
            // The internal "buoya" spelling reads as the numbered buoy; the "container" tag drops first.
            Assert.Equal("buoy A", Resolve("Buoya Container"));
            // Paired with a type word the type wins, so the alias never surfaces there.
            Assert.Equal("money", Resolve("Buoya Money"));
        }

        [Fact]
        public void Container_NewNounLogic_IsGatedToContainers()
        {
            // A prop that reads like a container keeps the prop noun extractor: "Box Backroom" stays the
            // last word for a prop, only the container path swaps it to the type word.
            Assert.Equal("backroom", Resolve("Box Backroom", cat: WorldTaxonomy.Interactable));
            Assert.Equal("box", Resolve("Box Backroom", cat: WorldTaxonomy.Container));
        }

        [Fact]
        public void LocationLeadingSlug_PrefersSpoilerSafeTitle()
        {
            // "Ice_eternite" - the noun extractor would guess the location "ice"; the title names it.
            Assert.Equal("Eternite", Resolve("Ice_eternite", title: "ICE / ETERNITE", cat: WorldTaxonomy.Interactable));
            Assert.Equal("Pile Of Eternite",
                Resolve("Eternite_door", title: "YARD / PILE OF ETERNITE", cat: WorldTaxonomy.Interactable));
        }

        [Fact]
        public void LocationLeadingSlug_UnsafeTitle_FallsBackToExtractedNoun()
        {
            // The title is rejected (a check word), so the pre-underscore token is spoken instead.
            Assert.Equal("stone", Resolve("stone_perc_1", title: "STONE PERC", cat: WorldTaxonomy.Interactable));
        }

        [Fact]
        public void EmptyName_NoTitle_FallsBackToCategoryWord()
        {
            Assert.Equal("container", Resolve("", cat: WorldTaxonomy.Container));
            Assert.Equal("object", Resolve(null, cat: WorldTaxonomy.Interactable));
        }

        [Fact]
        public void NamedCharacter_KeepsFullName()
        {
            Assert.Equal("Kim Kitsuragi", Resolve("Kim Kitsuragi", named: true, cat: WorldTaxonomy.Npc));
            Assert.Equal("Cunoesse", Resolve("Cunoesse", named: true, cat: WorldTaxonomy.Npc));
        }

        [Fact]
        public void NamedCharacter_HyphenatedName_IsKept()
        {
            // A real display name hyphenates with its capitals intact, so it is not a machine slug and is
            // spoken in full rather than reduced to the generic "person".
            Assert.Equal("Jean-Vicquemare", Resolve("Jean-Vicquemare", named: true, cat: WorldTaxonomy.Npc));
        }

        [Fact]
        public void NamedCharacterSlug_ReadsPerson_NeverTheTitle()
        {
            Assert.Equal("person",
                Resolve("npc_cunoesse", title: "CUNOESSE", named: true, cat: WorldTaxonomy.Npc));
        }

        [Fact]
        public void Door_KeepsCleanNameElseCategoryWord()
        {
            Assert.Equal("door", Resolve("courtyard-door-crypto-garys-apt", cat: WorldTaxonomy.Door));
            Assert.Equal("Whirling Door", Resolve("Whirling Door", cat: WorldTaxonomy.Door));
        }
    }
}
