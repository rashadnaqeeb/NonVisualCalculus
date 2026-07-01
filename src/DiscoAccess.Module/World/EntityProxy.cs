using DiscoAccess.Core.World;
using FortressOccident;
using LocalizationCustomSystem;
using PixelCrushers.DialogueSystem;
using Sunshine;
using Vector3 = System.Numerics.Vector3;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// The <see cref="IWorldItem"/> over a live <see cref="BasicEntity"/> (NPC, door, exit, container,
    /// prop). Reads everything live and classifies by the game's own <see cref="Interactable"/> subclass
    /// tree via <c>TryCast</c> (not GetType, which the interop boxes to BasicEntity).
    /// </summary>
    internal sealed class EntityProxy : IWalkTarget
    {
        // A footprint half-width is capped here so a pathological oversized renderer (an area-wide effect, a
        // skybox quad parented to an entity) can't become a footprint that swallows the cursor everywhere.
        private const float MaxFootprintHalf = 4f;

        private readonly BasicEntity _e;
        // The footprint's half-widths, computed once from the entity's combined renderer bounds. Size is
        // structural (an object's physical extent does not change), so it is measured once and cached, while
        // the box centre is read live from the transform each frame - a moving NPC's footprint still follows
        // it. Caching the size, not re-scanning every frame, is the deliberate divergence: a per-frame
        // GetComponentsInChildren across the ~90 accessible items would be far too costly, and the extent it
        // would return is the same constant each time.
        private float _halfX, _halfZ;
        private bool _footprintResolved;

        public EntityProxy(BasicEntity e) { _e = e; }

        // The spoken name, composed by Core from the raw fields plus the game's authored display name for this
        // thing (see AuthoredName): the destination area for an exit, else the actor that voices its examine
        // description. Core combines it with GameObject.name fallbacks. A Character is treated as a named thing
        // so a title is never spoken in its place.
        public string Name => EntityNaming.Resolve(_e.name, AuthoredName(), _e.conversation,
            _e.TryCast<Character>() != null, Category, SceneAreaTokens());
        public Vector3 Position => WorldConvert.ToSnv(_e.transform.position);

        // The real footprint: a Box on the XZ plane sized to the entity's combined renderer bounds, so the
        // cursor is "on" the thing anywhere over its surface, not only dead-centre. Centred on the live body
        // transform, so a moving NPC's footprint follows it. The cursor's hit test (ObjectCueSystem.Under) is
        // XZ-only, so a thing whose geometry sits up high - a staircase, an exit whose trigger origin floats
        // above the steps it spans - is still on the cursor when it glides beneath the footprint. An entity
        // with no renderers is a point at its body.
        public ScanBounds Bounds
        {
            get
            {
                EnsureFootprint();
                return _halfX > 0f || _halfZ > 0f
                    ? ScanBounds.Box(Position, _halfX, _halfZ)
                    : ScanBounds.Point(Position);
            }
        }

        // Measure the footprint half-widths once, from the union of the entity's SOLID mesh renderers. Only
        // enabled mesh and skinned-mesh renderers count: an entity drags along scattered effect renderers
        // (particles, projectiles, blob shadows, outline meshes) parented under it but positioned across the
        // scene, and encapsulating those blew a character's footprint out to the cap. The body mesh alone
        // gives the true footprint (a person reads ~0.6 m, a crate its box). Zero-bounds and disabled
        // renderers are skipped; the result is capped so nothing pathological swallows the cursor; an entity
        // with no mesh (a flat canvas billboard) stays a point. Resolved once (see the fields).
        private void EnsureFootprint()
        {
            if (_footprintResolved) return;
            _footprintResolved = true;
            var renderers = _e.GetComponentsInChildren<UnityEngine.Renderer>();
            if (renderers == null) return;
            bool any = false;
            UnityEngine.Bounds acc = default;
            for (int i = 0; i < renderers.Count; i++)
            {
                UnityEngine.Renderer r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r.TryCast<UnityEngine.MeshRenderer>() == null && r.TryCast<UnityEngine.SkinnedMeshRenderer>() == null)
                    continue; // an effect/particle/canvas renderer, not the body
                UnityEngine.Bounds b = r.bounds;
                if (b.size.x == 0f && b.size.y == 0f && b.size.z == 0f) continue;
                if (!any) { acc = b; any = true; } else acc.Encapsulate(b);
            }
            if (!any) return;
            _halfX = System.Math.Min(acc.extents.x, MaxFootprintHalf);
            _halfZ = System.Math.Min(acc.extents.z, MaxFootprintHalf);
        }

        // The game's authored display name for this thing, resolved live per type: an exit reads the localized
        // DESTINATION it leads to (so the player hears where a door goes, "Whirling-in-Rags"); everything else
        // reads the actor a sighted player sees as its examine header (see ExamineHeaderActor). An exit uses the
        // destination, not that header actor - an exit's examine can be narrated from the far side (a tent
        // flap whose description speaks as Andre), which would misname the door.
        private string AuthoredName()
            => Category == WorldTaxonomy.Exit ? ExitDestination() : ExamineHeaderActor();

        // What an exit's destination is called: its distinct localized area name when that differs from where
        // you are (another building, or a floor with its own name like "Bookstore"), else the floor/level word
        // (all Whirling floors share "Whirling-in-Rags", so "floor 2" / "basement" from the scene id). The
        // area scene id names the destination; its spawn-point id is not a place name. Null leaves the exit to
        // its own name or the plain type word. See EntityNaming.ExitDestinationLabel.
        private string ExitDestination()
        {
            var t = _e.TryCast<TransitionEntity>();
            string area = t?.area;
            if (string.IsNullOrEmpty(area)) return null;
            // A door onto the main exterior whose own name is a specific spot ("Balcony") should be named for
            // that spot, not the coarse "Martinaise": yield to the door name (Core turns it into "balcony door").
            if (area.Contains("-ext") && EntityNaming.SpotFromDoorName(_e.name) != null) return null;
            string destName = I2.Loc.LocalizationManager.GetTranslation("Area Names/" + area);
            string curArea = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string curName = I2.Loc.LocalizationManager.GetTranslation("Area Names/" + curArea);
            return EntityNaming.ExitDestinationLabel(area, destName, curName);
        }

        // The localized name a sighted player reads as this thing's examine header ("Cuno", "Pile of
        // Eternite", "Coupris Kineema"). The preferred source is the actor that voices the object's own first
        // examine-description line: for most examinables that speaker IS the object, which stays right even
        // when the conversation's ConversantID holds a narrative owner rather than the object (the Kineema's
        // conversant is Alice, the woman who owns it, while its self-description speaks as "Coupris Kineema").
        // But some examines open on one of the player's inner voices narrating the object instead of the
        // object speaking (the tire tracks open on a Visual Calculus passive); an inner voice is never the
        // object's name, so there we fall back to the ConversantID, which for those cases holds the object
        // itself ("Set of Tracks"). Read live through the dialogue database each call (never cached). Null
        // when the thing has no conversation, so Core falls back to the name noun.
        private string ExamineHeaderActor()
        {
            string conv = _e.conversation;
            if (string.IsNullOrEmpty(conv)) return null;
            DialogueDatabase db = DialogueManager.masterDatabase;
            Conversation c = db?.GetConversation(conv);
            if (c == null) return null;
            Actor speaker = db.GetActor(FirstDescriptionActor(c));
            if (speaker == null || IsInnerVoice(speaker)) speaker = db.GetActor(c.ConversantID);
            return speaker == null ? null : LocalizationUtils.GetActorLocalizedField(speaker, "Name");
        }

        // One of the player's inner voices - the four attributes, their skills, the Perception sub-senses, or
        // the player himself - rather than a world actor. DE renders each in an attribute palette colour (the
        // actor's "color" field, 2 through 5, and 7 for the player, who also reads IsPlayer), while every
        // object or NPC is colour 1 or unset. Such a voice can narrate an examine (a Visual Calculus passive
        // over the tire tracks) but is never the examined object's own name.
        private static bool IsInnerVoice(Actor a)
            => a.IsPlayer || InnerVoiceColors.Contains(a.LookupValue("color") ?? "");

        private static readonly System.Collections.Generic.HashSet<string> InnerVoiceColors =
            new System.Collections.Generic.HashSet<string> { "2", "3", "4", "5", "7" };

        // The actor id of the first entry carrying dialogue text reachable from the conversation's start node,
        // following outgoing links in author order (depth-first, so the first branch is walked before its
        // siblings). For most examinables that first spoken line's speaker is the object narrating itself; the
        // caller rejects the exception where it is one of the player's inner voices instead. Visited-set
        // guarded against cycles; a link that leaves this conversation is skipped (its target id is not in this
        // entry table). -1 when no text entry is reached.
        private static int FirstDescriptionActor(Conversation c)
        {
            var entries = c.dialogueEntries;
            if (entries == null || entries.Count == 0) return -1;

            var byId = new System.Collections.Generic.Dictionary<int, DialogueEntry>(entries.Count);
            DialogueEntry root = null;
            for (int i = 0; i < entries.Count; i++)
            {
                DialogueEntry e = entries[i];
                byId[e.id] = e;
                if (root == null && e.isRoot) root = e;
            }
            if (root == null && !byId.TryGetValue(0, out root)) root = entries[0];

            var stack = new System.Collections.Generic.Stack<int>();
            var seen = new System.Collections.Generic.HashSet<int>();
            stack.Push(root.id);
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                if (!seen.Add(id)) continue;
                if (!byId.TryGetValue(id, out DialogueEntry e)) continue;
                if (e.id != root.id && !string.IsNullOrEmpty(e.DialogueText)) return e.ActorID;
                var links = e.outgoingLinks;
                for (int i = links.Count - 1; i >= 0; i--)
                    stack.Push(links[i].destinationDialogueID);
            }
            return -1;
        }

        // The current scene's location stems, for Core to strip off the front of a slug-named container
        // ("martinaise-east-photo-of-rene" to "photo of rene"). The scene name is the English internal id
        // the same slugs are built from, so its word stems match; the generic level/type suffixes
        // (int/ext/f2/s1/antechamber) are not places and are dropped. Cheap and read live (naming runs only
        // for the item under the cursor, not the whole scan), so no caching.
        private static string[] SceneAreaTokens()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var toks = new System.Collections.Generic.List<string>();
            foreach (string part in scene.ToLowerInvariant().Split('-'))
            {
                if (part.Length < 2 || part == "int" || part == "ext" || part == "antechamber") continue;
                if ((part[0] == 'f' || part[0] == 's') && IsAllDigits(part, 1)) continue; // f2, s1
                toks.Add(part);
            }
            return toks.ToArray();
        }

        private static bool IsAllDigits(string s, int from)
        {
            if (from >= s.Length) return false;
            for (int i = from; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        public string Category => Classify(_e);
        public bool IsAccessible => _e.IsAccessible;

        // Whether a sighted player can currently see this thing. Interiors hide unentered rooms behind a
        // fog-of-war volume (Sunshine.Unseen.Zone, under "fow-unrevealers") rendered as black void; the
        // game's own MouseOverHighlight.RegisterAndTurnMeOff finds an entity's zone by casting straight up
        // on the fog layer, and this replicates that probe. UNSEEN is the never-entered black; ACTIVE and
        // INACTIVE (seen before, currently dimmed) are both knowable, so only UNSEEN hides. No zone above
        // (exteriors, revealed space) is visible. Read live, so a room reveals the frame its door opens.
        //
        // A body inside a fog volume is not the whole story: a BOUNDARY thing - a closed room's own door,
        // sitting in the wall - has its body under the room's volume yet is plainly seen (and knocked on)
        // from the corridor. Its interaction stand-point sits on the approach side, so a fogged body gets a
        // second probe there: stand-point in revealed space = visible boundary (Klaasje's corridor door);
        // stand-point also fogged = genuinely inside (her bathroom door, reachable only through the room).
        // The stand-point call is heavier, so it runs only for the few fogged bodies, not per item.
        public bool IsVisible
        {
            get
            {
                if (!HiddenAt(_e.transform.position)) return true;
                Party party = Party.Player;
                Character main = party != null ? party.Main : null;
                if (main == null) return false;
                return !HiddenAt(_e.GetInteractionLocation(LocationAt(WorldConvert.ToSnv(main.transform.position))).position);
            }
        }

        // Whether this point sits under an unrevealed room's fog volume (the game's own zone probe: cast up
        // on the fog layer, read the zone's status off the hit).
        private static bool HiddenAt(UnityEngine.Vector3 point)
        {
            if (!UnityEngine.Physics.Raycast(point, UnityEngine.Vector3.up,
                    out UnityEngine.RaycastHit hit, FogProbeDistance, FogLayerMask))
                return false;
            var zone = hit.collider.GetComponent<Sunshine.Unseen.Zone>();
            return zone != null && zone.status == Sunshine.Unseen.Zone.Status.UNSEEN;
        }

        // The fog layer the game's own zone probe casts against (MouseOverHighlight.RegisterAndTurnMeOff).
        private const int FogLayerMask = 0x2000;
        // Far enough to reach this room's fog volume overhead, short of the next floor up: the Whirling's
        // co-loaded floors stack ~6 m apart in world space, so a longer cast could hit the floor above's
        // volume and hide a thing that is in plain view.
        private const float FogProbeDistance = 4f;
        public bool RidesPlayer => false; // an entity is world-anchored, never carried by the character

        // The interaction stand-point and the reachability oracle, both approach-relative (computed from the
        // querying position). GameEntity, which BasicEntity derives from, supplies both; the from-position
        // becomes the Formation.Location the game measures the approach from.
        public Vector3 InteractionPoint(Vector3 from)
            => WorldConvert.ToSnv(_e.GetInteractionLocation(LocationAt(from)).position);

        public bool IsActionable(Vector3 from) => _e.CheckIfCanCreatePathToHavePath(LocationAt(from));

        // The IWalkTarget facts the Enter walk-then-interact verb needs beyond the sensing contract: the
        // stand-point's facing (so the character ends up looking the right way) and the game's own
        // arrival-range test. Kept here so the game-call and Unity<->Numerics conversion stay in the proxy.
        public Vector3 Approach(Vector3 from, out float heading)
        {
            Formation.Location loc = _e.GetInteractionLocation(LocationAt(from));
            heading = loc.heading;
            return WorldConvert.ToSnv(loc.position);
        }

        public bool WithinInteractionRadius(Vector3 playerPos)
            => _e.IsWithinInteractionRadius(WorldConvert.ToUnity(playerPos));

        // An entity says nothing extra on interact: the game reacts (opens a container, starts a conversation)
        // and its own readers speak. Only orbs float text the mod must voice itself.
        public string PostInteractLine() => null;

        private static Formation.Location LocationAt(Vector3 from)
            => new Formation.Location(WorldConvert.ToUnity(from), 0f);

        public bool Interact() => _e.Interact(new Interactable.ClickEventData());

        // Map the entity's runtime type onto a taxonomy category. Order matters where types nest (Curtains
        // derives from Door). TravelDestination/Teleporter exits derive from NavMeshClickHandler, not
        // BasicEntity, so they are not in sceneEntitySet and not seen here; TransitionEntity is.
        private static string Classify(BasicEntity e)
        {
            if (e.TryCast<Character>() != null) return WorldTaxonomy.Npc;
            if (e.TryCast<Door>() != null) return WorldTaxonomy.Door;
            if (e.TryCast<TransitionEntity>() != null) return WorldTaxonomy.Exit;
            if (e.TryCast<ContainerSource>() != null) return WorldTaxonomy.Container;
            return WorldTaxonomy.Interactable;
        }
    }
}
