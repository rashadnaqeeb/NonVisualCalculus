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

        // A collider union flat on an XZ axis below this is degenerate for the flat cursor (an upright click
        // plane like the eternite ice sheet's zero-depth box would leave an unhittable hairline), so it is
        // rejected in favour of the renderer fallback.
        private const float DegenerateHalf = 0.01f;

        private readonly BasicEntity _e;
        // The footprint's half-widths, computed once from the entity's click colliders or renderer bounds. Size is
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

        // The real footprint: a Box on the XZ plane sized to the entity's click colliders (or, failing those,
        // its renderer bounds), so the cursor is "on" the thing anywhere a sighted player could click it,
        // not only dead-centre. Centred on the live body
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

        // Measure the footprint half-widths once (see the fields), preferring the entity's click colliders
        // over its renderers. The colliders ARE the shape a sighted player clicks - the game's own mouse
        // picking raycasts against them - and they are authored padded for clickability (a coin's collider
        // reads ~0.5 m across over a ~0.2 m mesh), which is exactly the forgiveness the cursor wants. They
        // also stay tight where renderer bounds inflate (a skinned character's animation-swept AABB). The
        // renderer sweep remains the fallback for the few entities with no usable collider.
        private void EnsureFootprint()
        {
            if (_footprintResolved) return;
            _footprintResolved = true;
            if (ColliderFootprint()) return;
            RendererFootprint();
        }

        // The union AABB of the enabled colliders the game's mouse picking hits this entity through: each
        // MouseOverHighlight under the entity gathers its own Collider[] at startup, so reading that array
        // reuses the game's curated click shape rather than re-guessing which colliders are the body. False
        // (fall back to renderers) when there is no highlight or no enabled collider, or when the union is
        // flat on an XZ axis (see DegenerateHalf) - an upright click plane reads fine to a mouse ray but
        // has no footprint on the flat cursor's map.
        private bool ColliderFootprint()
        {
            var highlights = _e.GetComponentsInChildren<MouseOverHighlight>();
            if (highlights == null) return false;
            bool any = false;
            UnityEngine.Bounds acc = default;
            for (int i = 0; i < highlights.Count; i++)
            {
                var colliders = highlights[i].m_collider;
                if (colliders == null) continue;
                for (int j = 0; j < colliders.Count; j++)
                {
                    UnityEngine.Collider c = colliders[j];
                    if (c == null || !c.enabled) continue;
                    UnityEngine.Bounds b = c.bounds;
                    if (b.size.x == 0f && b.size.y == 0f && b.size.z == 0f) continue;
                    if (!any) { acc = b; any = true; } else acc.Encapsulate(b);
                }
            }
            if (!any || acc.extents.x < DegenerateHalf || acc.extents.z < DegenerateHalf) return false;
            _halfX = System.Math.Min(acc.extents.x, MaxFootprintHalf);
            _halfZ = System.Math.Min(acc.extents.z, MaxFootprintHalf);
            return true;
        }

        // The fallback: the union of the entity's SOLID mesh renderers. Only enabled mesh and skinned-mesh
        // renderers count: an entity drags along scattered effect renderers (particles, projectiles, blob
        // shadows, outline meshes) parented under it but positioned across the scene, and encapsulating
        // those blew a character's footprint out to the cap. The body mesh alone gives the true footprint
        // (a person reads ~0.6 m, a crate its box). Zero-bounds and disabled renderers are skipped; the
        // result is capped so nothing pathological swallows the cursor; an entity with no mesh (a flat
        // canvas billboard) stays a point.
        private void RendererFootprint()
        {
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
        // Eternite", "Coupris Kineema"). An entity whose GameObject name IS a participant's internal actor
        // name names itself by that identity first: two people can share one examine conversation (the
        // plaza's "fuckTheWorld" and "pissflaubert" both open PLAZA / PISSFLAUBERT AND FTW), where any walk
        // of the shared lines names both entities after the same speaker. Otherwise two-step, keyed on what
        // the conversation's ConversantID holds:
        //
        // A NON-PERSON conversant (no IsNPC field on the actor, the DE data's person marker) IS the examined
        // object by construction ("Pile of Clothes", "Set of Tracks", "Shack Door"), so it names the thing
        // directly. The first examine line is NOT trusted here: a person can voice an object's opening line
        // (Siileng pitching the wares on his stall, Kim remarking on the yard footprints), and even another
        // object-actor can ("A Pedestal of Speakers" opening for the FALN sneakers displayed on it), all of
        // which would misname the thing.
        //
        // A PERSON conversant means the conversation belongs to a character (named by whoever voices its
        // first description line, normally themselves) or to an object with a narrative owner (the Kineema's
        // conversant is Alice, the woman who owns it, while its self-description speaks as "Coupris
        // Kineema") - so there the first-line speaker names the thing, falling back to the conversant when
        // that speaker is one of the player's inner voices narrating (an inner voice is never the object's
        // name). Read live through the dialogue database each call (never cached). Null when the thing has
        // no conversation, so Core falls back to the name noun.
        private string ExamineHeaderActor()
        {
            DialogueDatabase db = DialogueManager.masterDatabase;
            if (db == null) return null;
            if (ExamineActorOverrides.TryGetValue(_e.name, out string overrideActor))
            {
                Actor named = db.GetActor(overrideActor);
                if (named != null) return LocalizationUtils.GetActorLocalizedField(named, "Name");
            }
            string conv = _e.conversation;
            if (string.IsNullOrEmpty(conv)) return null;
            Conversation c = db.GetConversation(conv);
            if (c == null) return null;
            Actor conversant = db.GetActor(c.ConversantID);
            if (conversant != null && EntityNaming.NameMatchesActor(_e.name, conversant.Name))
                return LocalizationUtils.GetActorLocalizedField(conversant, "Name");
            Actor cast = db.GetActor(c.ActorID);
            if (cast != null && EntityNaming.NameMatchesActor(_e.name, cast.Name))
                return LocalizationUtils.GetActorLocalizedField(cast, "Name");
            if (conversant != null && !IsPerson(conversant) && !IsInnerVoice(conversant))
                return LocalizationUtils.GetActorLocalizedField(conversant, "Name");
            Actor speaker = db.GetActor(FirstDescriptionActor(c));
            if (speaker == null || IsInnerVoice(speaker)) speaker = conversant;
            return speaker == null ? null : LocalizationUtils.GetActorLocalizedField(speaker, "Name");
        }

        // The dialogue actor whose localized name IS a thing's examine header, for the few objects the
        // two-step above cannot reach: Klaasje's door has no conversant and opens on the player's inner
        // voices, yet the game names it - "Door, Room #3" speaks its lines deeper in the conversation.
        // Curated per object (keyed by GameObject.name) so the name still comes from the game's own
        // localized actor table, never authored by the mod.
        private static readonly System.Collections.Generic.Dictionary<string, string> ExamineActorOverrides =
            new System.Collections.Generic.Dictionary<string, string>
        {
            ["Whirling Door Klaasje"] = "Door, Room #3",
        };

        // A person in the dialogue data: the player, or an actor the game marks IsNPC (people carry it,
        // object-actors lack it; the talking Kineema is marked IsNPC but never reaches the checks that
        // need this distinction, since it is a conversant only through Alice).
        private static bool IsPerson(Actor a)
            => a.IsPlayer || string.Equals(a.LookupValue("IsNPC"), "true", System.StringComparison.OrdinalIgnoreCase);

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

        // Classified once per proxy: the category is structural (an entity never changes class or grows a
        // skinned body), and the sonar reads it per ping across every tracked item, which the body test's
        // child-renderer sweep is too costly for (the same trade the footprint cache makes).
        public string Category => _category ??= Classify(_e);
        private string _category;
        public bool IsAccessible => _e.IsAccessible;

        // Whether a sighted player can currently see this thing. Interiors hide unentered rooms behind
        // fog-of-war volumes rendered as black void; FogSense reads what the volume over a point says.
        // The volumes' meshes cover rooms' open interiors but stop at walls, so a wall-recessed body (the
        // bathroom medicine cabinet, pivot sunk 0.3 m past the volume's rim) reads no zone at all - "no
        // volume overhead" proves a thing seeable only together with clear distance from every volume
        // (FogSense.NearVolume). Read live, so a room reveals the frame its door opens.
        //
        // A CROSSING (door, exit) is seen from whichever side is revealed: its body hangs under the room's
        // volume, so it is offered when any cardinal face pokes into revealed space (the closed bathroom
        // door, knocked on from the corridor) and hidden when every face is unseen (the same door looked
        // for from beyond the still-black main room).
        //
        // Anything else under or hard against an unseen volume is judged by its APPROACH: an interaction
        // stand-point (the spot the player would walk to in order to act) in unseen space means genuinely
        // inside the unrevealed room (the medicine cabinet). When the player is already within the thing's
        // interaction radius the game answers with the player's own spot - no approach information, since
        // the radius passes through walls - so there the body's own reading decides alone: only a fogged
        // body hides. Face evidence is NOT consulted in that case - a revealed wall-mounted thing's faces
        // poke through its wall into the neighbouring room's fog (the shared bathroom's mirror backs onto
        // Kitsuragi's unseen room), which would hide a thing in plain view. The stand-point call is heavier
        // (it can run a navmesh path), so it runs only for unseen-adjacent bodies.
        public bool IsVisible
        {
            get
            {
                UnityEngine.Vector3 body = _e.transform.position;
                FogSense.ZoneState at = FogSense.At(body);
                if (at == FogSense.ZoneState.Knowable) return true;

                string cat = Category;
                if (cat == WorldTaxonomy.Door || cat == WorldTaxonomy.Exit)
                    return at == FogSense.ZoneState.None || AnyFaceRevealed(body);

                if (at == FogSense.ZoneState.None && !FogSense.NearVolume(body)) return true;

                Party party = Party.Player;
                Character main = party != null ? party.Main : null;
                if (main == null) return false;
                UnityEngine.Vector3 from = main.transform.position;
                UnityEngine.Vector3 stand = _e.GetInteractionLocation(LocationAt(WorldConvert.ToSnv(from))).position;
                if ((stand - from).sqrMagnitude > StandpointEpsilon * StandpointEpsilon)
                    return FogSense.At(stand) != FogSense.ZoneState.Unseen;
                return at != FogSense.ZoneState.Unseen;
            }
        }

        // The cardinal-face probes: a step out from the body on each XZ cardinal, reaching past a wall
        // recess or a doorway line to whatever volume covers the neighbouring space.
        private static bool AnyFaceRevealed(UnityEngine.Vector3 p)
        {
            for (int i = 0; i < 4; i++)
                if (FogSense.At(Face(p, i)) != FogSense.ZoneState.Unseen) return true;
            return false;
        }

        private static UnityEngine.Vector3 Face(UnityEngine.Vector3 p, int i)
        {
            switch (i)
            {
                case 0: return new UnityEngine.Vector3(p.x + FogEdgeReach, p.y, p.z);
                case 1: return new UnityEngine.Vector3(p.x - FogEdgeReach, p.y, p.z);
                case 2: return new UnityEngine.Vector3(p.x, p.y, p.z + FogEdgeReach);
                default: return new UnityEngine.Vector3(p.x, p.y, p.z - FogEdgeReach);
            }
        }

        // How far a face probe steps out from the body: past a wall recess or a door panel's thickness,
        // short of skipping a whole wall into the next room over.
        private const float FogEdgeReach = 0.5f;
        // A stand-point this close to the queried position is the game's within-radius shortcut answering
        // with the query itself, not a computed approach.
        private const float StandpointEpsilon = 0.05f;
        public bool RidesPlayer => false; // an entity is world-anchored, never carried by the character

        // The spot the game's click would walk the player to: the nearest authored INTERACTION marker when
        // the entity carries one - the destination MoveToTarget actually prices - else the radius-searched
        // interaction location computed from the querying position. Marker-first because the radius search
        // can hand back a spot on the wrong level (a balcony point for the street door under it), which
        // would misstate the spoken distance and bearing.
        public Vector3 InteractionPoint(Vector3 from)
        {
            FormationMarker marker = NearestInteractionMarker(from);
            return marker != null
                ? WorldConvert.ToSnv(marker.transform.position)
                : WorldConvert.ToSnv(_e.GetInteractionLocation(LocationAt(from)).position);
        }

        // The discovery gates' reachability test (see IWorldItem.ReachableFrom).
        //
        // A PERSON, and any thing with authored INTERACTION FormationMarkers, is offered exactly when the
        // game's own click would act (see ClickWouldAct): the click prices the walk to those authored
        // stand-spots, which the geometric tests below cannot judge - the Smoker on the Balcony is
        // interrogated from the yard four metres under him (authored gameplay, per the Logic orb's "Talk to
        // him"), and Garte behind his counter island is walked to from the customer side. The verdict keeps
        // both and drops what every path is walled off from (Cuno behind the yard fence) exactly while the
        // game's click refuses it too. A markerless thing is NOT priced: the pricing then falls back to the
        // same radius-grabbed interaction location that makes IsActionable lie (see below), so geometry
        // stays its truth.
        //
        // A markerless thing asks for standing ground: the nearest walkable mesh the body stands on,
        // walk-connected to the reference by a COMPLETE navmesh path - the stairs triggers connect via their
        // own steps, the plaza below the Whirling balcony is a separate island. A body with no walkable mesh
        // under it at all is grounded at its edge instead (MooredGround): the fishing village's Motorboat
        // floats on water, which carries no navmesh, but its gunwale meets the walkway a sighted player
        // clicks it from. The game's click oracle (IsActionable) is deliberately not consulted: its
        // stand-point search is a 3D radius that can grab a spot on an unrelated level over the thing's
        // head (the Whirling front door's 3 m radius reaches the balcony floor above it, and the oracle
        // then paths two metres to the balcony edge and calls the ground-floor door reachable).
        public bool ReachableFrom(Vector3 from)
        {
            if (Category == WorldTaxonomy.Npc || InteractionMarkers.Length > 0) return ClickWouldAct();
            if (!StandingGround(out UnityEngine.Vector3 ground) && !MooredGround(from, out ground))
                return false;
            var path = new UnityEngine.AI.NavMeshPath();
            return UnityEngine.AI.NavMesh.CalculatePath(WorldConvert.ToUnity(from), ground, -1, path)
                   && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
        }

        // The game's click verdict, priced without acting. Clicking any entity runs
        // GameController.MoveToTarget, which prices a MovementCommand to the entity's authored
        // FormationMarker stand-spots (the cheapest INTERACTION-purpose marker; the interaction location
        // when it has none) and refuses while the cheapest price stays infinite - that refusal is the
        // "can't reach" a walk-interact would otherwise discover only after walking. Pricing a fresh
        // command writes nothing but the command's own fields, so asking is side-effect free. The verdict
        // sees what the geometric tests cannot: the balcony Smoker's authored spot prices finite from the
        // yard below him (the radius oracle IsActionable never reads markers and wrongly says no), while
        // Cuno prices infinite from beyond the yard fence and finite once the player rounds it. Priced
        // from the party's live formation, which is also the reference position every discovery gate
        // passes as from.
        private bool ClickWouldAct()
        {
            Party party = Party.Player;
            GameController gc = GameController.Singleton;
            if (party == null || gc == null) return false;
            var command = new MovementCommand(party, false);
            command.purpose = Formation.Purpose.INTERACTION;
            command.Process(_e, gc.isNarrowEnvironment ? Sector.behind : Sector.left);
            return !float.IsPositiveInfinity(command.cost);
        }

        // The entity's authored INTERACTION stand-spots, gathered the way the click's pricing does
        // (FormationMarkers on the entity or a parent, filtered by purpose). The component set is authored
        // scene structure, resolved once per proxy (the footprint and category trade); each is a live
        // component whose position is read at query time.
        private FormationMarker[] InteractionMarkers
        {
            get
            {
                if (_interactionMarkers == null)
                {
                    var found = new System.Collections.Generic.List<FormationMarker>();
                    var markers = _e.GetComponentsInParent<FormationMarker>();
                    if (markers != null)
                        foreach (FormationMarker m in markers)
                            if (m != null && m.HasPurpose(new[] { Formation.Purpose.INTERACTION }))
                                found.Add(m);
                    _interactionMarkers = found.ToArray();
                }
                return _interactionMarkers;
            }
        }
        private FormationMarker[] _interactionMarkers;

        private FormationMarker NearestInteractionMarker(Vector3 from)
        {
            FormationMarker best = null;
            float bestD = float.MaxValue;
            foreach (FormationMarker m in InteractionMarkers)
            {
                float d = Vector3.DistanceSquared(WorldConvert.ToSnv(m.transform.position), from);
                if (d < bestD) { bestD = d; best = m; }
            }
            return best;
        }

        // The walkable mesh this thing is MOORED AGAINST, for a body with no standing ground of its own: a
        // thing over unwalkable surface (the Motorboat on the water off the fishing village walkway) belongs
        // to the ground its clickable edge meets, sampled at the footprint point nearest the reference - the
        // same widening OrbProxy applies to an orb over water. Bounded by the one sample radius, so only
        // ground within arm's reach of the edge is adopted; whether that ground is the player's own is the
        // connectivity test's call, not repeated here. StandingGround's floor band does not apply: relative
        // to a waterline pivot the walkway IS overhead (the boat reads 1.4 m below the pier), which is
        // exactly the geometry this fallback exists to accept.
        private bool MooredGround(Vector3 from, out UnityEngine.Vector3 ground)
        {
            UnityEngine.Vector3 edge = WorldConvert.ToUnity(Bounds.NearestPoint(from));
            if (UnityEngine.AI.NavMesh.SamplePosition(edge, out var hit, GroundSampleRadius, -1))
            {
                ground = hit.position;
                return true;
            }
            ground = default;
            return false;
        }

        // The walkable mesh this thing STANDS ON: sampled from the body downward in steps, accepting only a
        // hit inside the body's own floor band - a nearest-mesh sample alone grabs the platform overhead
        // where levels stack tight (the Whirling front door sits in a wall the street mesh is cut back from,
        // so its nearest mesh is the balcony floor 2 m over its head), and an uncapped descent adopts the
        // storey below (Tequila's bedroom door on the Whirling's meshless mezzanine would ground on the bar
        // floor 3.6 m under it). Descending restarts shift the search below an overhead platform to the real
        // floor (the street under the door, the steps under a floating exit trigger). False when no mesh
        // lands in the band - a display-only landing or a thing hanging over void has no standing ground.
        private bool StandingGround(out UnityEngine.Vector3 ground)
        {
            UnityEngine.Vector3 body = _e.transform.position;
            for (float drop = 0f; drop <= GroundMaxDrop; drop += 1f)
            {
                var probe = new UnityEngine.Vector3(body.x, body.y - drop, body.z);
                if (UnityEngine.AI.NavMesh.SamplePosition(probe, out var hit, GroundSampleRadius, -1)
                    && hit.position.y <= body.y + GroundHeadroom
                    && body.y - hit.position.y <= GroundMaxDrop)
                {
                    ground = hit.position;
                    return true;
                }
            }
            ground = default;
            return false;
        }

        // Per-step sample radius: wide enough to reach the floor beside a wall-mounted thing, narrow enough
        // that the descent (not the radius) does the work of skipping an overhead platform.
        private const float GroundSampleRadius = 2f;
        // The floor band below the body that can be its own standing ground: a door's threshold sits up to
        // ~1.8 m under its mid-panel pivot and a floating exit trigger up to ~2 m over its steps (the
        // Whirling courtyard and stairs triggers), while the next storey down starts ~3.5 m (the bar floor
        // under the mezzanine bedroom door). Also bounds the probe descent - deeper hits are all rejected.
        private const float GroundMaxDrop = 2.5f;
        // A hit this little above the body is its own floor plane (a pivot sunk into a hatch or rug); more is
        // a platform overhead, never standing ground.
        private const float GroundHeadroom = 0.5f;

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

        // An entity's Interact IS the game's click: it prices the approach, walks the whole party
        // (re-pathing live toward a moving target), and fires the interaction on arrival, so the walk verb
        // fires it and stands back. The run flag is the click flow's own double-click.
        public bool InteractWalks => true;
        public bool Interact() => Interact(run: false);

        public bool Interact(bool run)
        {
            var data = new Interactable.ClickEventData { isDoubleClick = run };
            return _e.Interact(data);
        }

        // Map the entity's runtime type onto a taxonomy category. Order matters where types nest (Curtains
        // derives from Door). TravelDestination/Teleporter exits derive from NavMeshClickHandler, not
        // BasicEntity, so they are not in sceneEntitySet and not seen here; TransitionEntity is.
        //
        // A Character is only a person when its body is a skinned mesh: every person is an animated
        // skinned-mesh rig, including the agentless, animatorless ones (the gardener, Tommy Lhomme). The
        // game also builds talking props - the Coupris Kineema, a mailbox, the waiting bench, the yard's
        // trash container - as Character so they can hold a conversation, but those render as static
        // meshes on the entity root with no skinned renderer anywhere under them, and to the player they
        // are things in the world, not people. Components are NOT the body test: CharacterAnimator misses
        // the gardener and Tommy, and a walking-body collider check (NavMeshAgent or CapsuleCollider)
        // claimed the trash container, whose click collider is authored as a capsule.
        private static string Classify(BasicEntity e)
        {
            if (e.TryCast<Character>() != null)
            {
                var skinned = e.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>();
                return skinned != null && skinned.Count > 0
                    ? WorldTaxonomy.Npc : WorldTaxonomy.Interactable;
            }
            if (e.TryCast<Door>() != null) return WorldTaxonomy.Door;
            if (e.TryCast<TransitionEntity>() != null) return WorldTaxonomy.Exit;
            if (e.TryCast<ContainerSource>() != null) return WorldTaxonomy.Container;
            return WorldTaxonomy.Interactable;
        }
    }
}
