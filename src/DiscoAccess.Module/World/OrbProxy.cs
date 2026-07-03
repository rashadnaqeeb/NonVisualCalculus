using DiscoAccess.Core.Text;
using DiscoAccess.Core.World;
using FortressOccident;
using PixelCrushers.DialogueSystem;
using Sunshine;
using Vector3 = System.Numerics.Vector3;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// The <see cref="IWalkTarget"/> over a live <see cref="SenseOrb"/> (a clue/thought orb in the scene). The
    /// cursor senses a world-anchored orb once its conditions are met (or it offers a morsel teaser), names it
    /// from its clue text (<see cref="OrbNaming"/>), and the Enter verb walks the character into range and
    /// triggers it. Everything reads live off the orb (the "never cache game state" rule).
    /// </summary>
    internal sealed class OrbProxy : IWalkTarget
    {
        // The footprint disc for any orb is its small drawn body - the thing a sighted player sees and
        // clicks - never its InteractionRadius trigger range (up to 16 m): a footprint that wide reads
        // distance-zero across a whole plaza, dominating the cursor everywhere near the orb, while the
        // radius still governs the actual trigger range (Interact, WithinInteractionRadius). For a
        // thought-cabinet orb riding the character the same disc is the near-player footprint: wide enough
        // that the cursor centred on the character is on it, tight enough not to shadow a real interactable
        // next to the player.
        private const float BodyRadius = 0.5f;

        // How far the body disc may widen to reach walkable ground (see FootprintRadius), the orb mirror of
        // an entity's MaxFootprintHalf, so an orb far off the mesh cannot regrow a plaza-wide footprint.
        private const float MaxFootprintRadius = 4f;

        private readonly SenseOrb _orb;
        // Resolved once (see FootprintRadius): the widening measures the orb's centre against the static
        // navmesh and a world-anchored orb does not move, so the radius is structural - the same
        // size-not-state caching EntityProxy does; the disc centre is still read live.
        private float _footprintRadius = -1f;

        public OrbProxy(SenseOrb orb) { _orb = orb; }

        public string Name => OrbNaming.Resolve(_orb.textOverride, MorselText(), _orb.conversation,
            RidesPlayer, GameLocalization.IsEnglish);
        public Vector3 Position => WorldConvert.ToSnv(_orb.transform.position);

        // The footprint is the body disc - the same click-target treatment an entity gets from its click
        // colliders - widened for a world orb whose centre sits off the walkable mesh (out over water, a
        // gap): the cursor is clamped to walkable ground, so a bare body disc there could never be under it,
        // and the disc grows just enough to reach the nearest walkable point. The hit test is XZ-only
        // (ObjectCueSystem), so height is folded away here exactly as for an entity.
        public ScanBounds Bounds => ScanBounds.Circle(Position, FootprintRadius());

        private float FootprintRadius()
        {
            if (_footprintRadius >= 0f) return _footprintRadius;
            _footprintRadius = BodyRadius;
            if (!IsThoughtFamily)
            {
                UnityEngine.Vector3 body = WorldConvert.ToUnity(Position);
                // NavMesh.AllAreas (-1); the const isn't surfaced on the interop proxy. Sampled past the
                // widening cap because the sample distance is 3D while the widening is XZ-only: a high
                // orb must still find the ground straight under it (XZ offset ~0) rather than fail the
                // sample and widen to the cap. No walkable ground within 8 m means no cursor can ever be
                // near, so the radius then does not matter and the bare body disc stands.
                if (UnityEngine.AI.NavMesh.SamplePosition(body, out var hit, 8f, -1))
                {
                    float dx = hit.position.x - body.x, dz = hit.position.z - body.z;
                    float reach = (float)System.Math.Sqrt(dx * dx + dz * dz) + BodyRadius;
                    _footprintRadius = System.Math.Min(reach, MaxFootprintRadius);
                }
            }
            return _footprintRadius;
        }
        public string Category => WorldTaxonomy.Orb;

        // What the cursor reports, in two flavours. An orb already triggered is excluded from both (WasShown):
        // the game's own IsAccessible reflects only prerequisites/skill, never whether the orb has been read,
        // so without this a shown orb stays under the cursor forever - reads its clue on Enter but never leaves,
        // the freshness the sighted player sees fade away.
        //
        // A WORLD-ANCHORED orb (map/orbital/dick) sits at a fixed spot; it is reported when its gameplay
        // conditions are met or it offers a morsel teaser - the orb-side equivalent of an entity's IsAccessible
        // flag. Draw state (whether it is currently rendered/orbiting) is deliberately NOT required: the cursor
        // is the blind player's eyes, so an accessible-but-undrawn orb (an orbital orb the character has not
        // walked up to yet, like the halogen-watermark orb read from across the plaza) must still be findable.
        // A DORMANT Modus Mullen orb is excluded (see IsMullenDormant): outside that hidden minigame it is an
        // inactive husk with no clickable orbUI, so Interact could not trigger it and it would sit unclearable -
        // but once the minigame activates it (ShowOrbs gives it a live orbUI) it becomes a real target again.
        //
        // A PLAYER-ANCHORED thought-cabinet orb (afterthought/obsession/paralyzer/thought) orbits the character
        // rather than sitting in the world; it is the only way to un-paralyze or complete a thought, or to read
        // the orb an equipped item raises, so it must be reachable. Here the live orbUI IS the gate: unlike a
        // world orb (drawn only when walked up to), a character orb is always in view, so an orbUI means the
        // game is showing it as clickable right now - active and correctly triggerable through OrbUiElement.Open
        // (which alone does the thought/paralyzer removal). With no orbUI the orb is a dormant pool husk, so it
        // is not reported. Only one is ever active at a time, so they do not stack under the cursor.
        public bool IsAccessible => !_orb.WasShown() && (WorldAnchoredReady || PlayerAnchoredReady);

        private bool WorldAnchoredReady
            => IsWorldAnchored && !IsMullenDormant && (_orb.IsAccessible || _orb.IsMorsel);
        private bool PlayerAnchoredReady => IsThoughtFamily && _orb.orbUI != null;

        // A Modus Mullen orb that the minigame has not activated: it carries no orbUI, so the game gives it no
        // click target and our Interact has nothing to Open. Scoped to Mullen orbs - a non-Mullen clue orb
        // deliberately needs no live orbUI to be findable (it gains one when the character walks up to it,
        // which a dormant Mullen orb never does).
        private bool IsMullenDormant => _orb.isMullenOrb && _orb.orbUI == null;

        // An orb under an unrevealed room's fog volume is invisible like anything else there; a
        // player-anchored orb rides the character, whose own room is by definition revealed.
        public bool IsVisible
            => IsAccessible && FogSense.At(_orb.transform.position) != FogSense.ZoneState.Unseen;

        // A thought-cabinet orb rides the character, so the cursor's near-player skip must spare it.
        public bool RidesPlayer => IsThoughtFamily;

        // An orb has no open/closed state.
        public bool IsOpen => false;

        private bool IsWorldAnchored
            => _orb.orbType == OrbType.MAP || _orb.orbType == OrbType.ORBITAL || _orb.orbType == OrbType.DICK;

        // The thought-cabinet family, which orbits the character instead of anchoring to the world.
        private bool IsThoughtFamily
            => _orb.orbType == OrbType.AFTERTHOUGHT || _orb.orbType == OrbType.OBSESSION
               || _orb.orbType == OrbType.PARALYZER || _orb.orbType == OrbType.THOUGHT;

        // The ground the gather walk would END on, for parking the cursor. The walk drives at Approach's
        // navmesh snap, but that snap is the 3D-nearest mesh to a floating body and can sit on a severed
        // island the character can never stand on (the window-curtains orb's nearest mesh is the balcony
        // over the yard it is authored to be read from). What the character actually reaches is the end
        // of the path priced from the player toward the snap - complete (the snap itself) or partial (the
        // player-island spot the walk stops on, inside the trigger sphere for an offered orb: the same
        // partial-endpoint test ReachableFrom gates on). The snap stands as the fallback when no path
        // prices at all.
        public Vector3 InteractionPoint(Vector3 from)
        {
            Vector3 stand = Approach(from, out _);
            var path = new UnityEngine.AI.NavMeshPath();
            if (UnityEngine.AI.NavMesh.CalculatePath(WorldConvert.ToUnity(from), WorldConvert.ToUnity(stand), -1, path))
            {
                var corners = path.corners;
                if (corners.Length > 0) return WorldConvert.ToSnv(corners[corners.Length - 1]);
            }
            return stand;
        }

        // Whether the walk can put the character inside this orb's trigger sphere. The game's trigger test
        // is a full 3D sphere (GameEntity.IsWithinInteractionRadius compares squared 3D distance), so height
        // counts: a wide sphere reaches the ground below a balcony (the window-curtains orb, radius 9 at
        // 5.7 m up, is read from the yard - the authored from-below reads keep working), while a tight
        // sphere overhead meets no reachable ground at all (the smoker apartment door orb, radius 2.5 at
        // 7.3 m up, is a balcony read; offering it from the yard promised a trigger the game refuses, and
        // the flat arrival test would even let Open fire it through the floor). Reachable, then: a complete
        // walk to the spot the trigger snaps to, or a partial walk whose endpoint still falls inside the
        // sphere - an offered orb is thereby always flat-arrivable too (flat distance never exceeds 3D).
        // A thought-family orb rides the character and needs no walk.
        public bool ReachableFrom(Vector3 from)
        {
            if (IsThoughtFamily) return true;
            // A DRAWN orb is actable from anywhere: the sighted click (OrbUiElement.OnPointerClicked) fires
            // Open in place with no walk and no range test, and Interact below does the same, so pathability
            // is moot while its UI is live.
            if (_orb.orbUI != null) return true;
            UnityEngine.Vector3 body = WorldConvert.ToUnity(Position);
            float radius = _orb.InteractionRadius;
            UnityEngine.Vector3 target = body;
            // Same snap the walk verb drives at (Approach), so the gate judges the walk it would take.
            if (UnityEngine.AI.NavMesh.SamplePosition(body, out var snap, System.Math.Max(radius, 1f), -1))
                target = snap.position;
            var path = new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(WorldConvert.ToUnity(from), target, -1, path))
                return false;
            if (path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete) return true;
            var corners = path.corners;
            return corners.Length > 0
                   && (corners[corners.Length - 1] - body).sqrMagnitude <= radius * radius;
        }

        // Walk to a walkable spot at the orb body's footprint. An orb can float above the mesh, so snap its
        // position onto the navmesh within its interaction radius; failing that, drive at the body itself and
        // let the walk stall into a can't-reach. The heading faces the orb from the stand-point.
        public Vector3 Approach(Vector3 from, out float heading)
        {
            Vector3 body = Position;
            Vector3 stand = body;
            float snap = System.Math.Max(_orb.InteractionRadius, 1f);
            // NavMesh.AllAreas (-1, every area); the const isn't surfaced on the interop proxy.
            if (UnityEngine.AI.NavMesh.SamplePosition(WorldConvert.ToUnity(body), out var hit, snap, -1))
                stand = WorldConvert.ToSnv(hit.position);
            float dx = body.X - stand.X, dz = body.Z - stand.Z;
            heading = (float)(System.Math.Atan2(dx, dz) * (180.0 / System.Math.PI)); // Y-euler facing the orb
            return stand;
        }

        // Arrival is a flat-map question, matching the cursor's XZ footprint model and the orb gather (which
        // is measured on the floor): an orb floating overhead is "reached" when the character stands under its
        // interaction circle, not only when 3D-close, which the ground character could never be.
        public bool WithinInteractionRadius(Vector3 playerPos)
        {
            // A thought-cabinet orb rides the character and reports a zero interaction radius, so the character
            // is always under it - no walking needed, and the distance test below would spuriously fail on the
            // zero radius. Trigger it in place.
            if (IsThoughtFamily) return true;
            float dx = playerPos.X - Position.X, dz = playerPos.Z - Position.Z;
            return dx * dx + dz * dz <= _orb.InteractionRadius * _orb.InteractionRadius;
        }

        // A DRAWN orb acts in place from any distance - the sighted click is OrbUiElement.OnPointerClicked,
        // which fires Open directly with no walk and no range test - so the walk verb fires Interact and
        // speaks the in-place result instead of driving a walk. An UNDRAWN world orb has no click target
        // for a sighted player either; walking into its trigger sphere is the blind affordance for the same
        // gather mechanic, so the verb's watched walk stays for it. The pace flag has nothing to steer.
        public bool InteractWalks => _orb.orbUI != null;
        public bool Interact(bool run) => Interact();

        // Trigger the orb through the game's own orb click (OrbUiElement.Open): a simple orb floats its text
        // (spoken by PostInteractLine), a dialogue orb opens its conversation (read by the dialogue screen),
        // and both mark it shown and update visuals - which a bare StartConversation would skip, leaving a
        // simple orb's float text unshown. Drawn: fire from wherever the character stands, the sighted
        // click's own behaviour. Undrawn: only in range (the walk verb has walked the character into the
        // trigger sphere), where a world orb falls back to its conversation so an undrawn dialogue orb still
        // reads; a thought-cabinet orb can only be triggered through Open (which alone runs the
        // thought/paralyzer removal), so it refuses and the caller logs the miss rather than mis-trigger.
        public bool Interact()
        {
            var ui = _orb.orbUI;
            if (ui != null) { ui.Open(); return true; }
            Party party = Party.Player;
            Character main = party != null ? party.Main : null;
            if (main == null) return false;
            if (!WithinInteractionRadius(WorldConvert.ToSnv(main.transform.position))) return false;
            if (IsThoughtFamily) return false;
            DialogueManager.StartConversation(_orb.conversation);
            return true;
        }

        // What to speak right after triggering. A simple orb floats its clue as a world label (SpawnFloatText)
        // that no dialogue screen or bark reader carries, and that float path cannot be Harmony-hooked (the
        // method is inlined), so the mod voices the text itself here, mirroring GetText's source order
        // (textOverride, else the morsel field, else the conversation's Description) with the localized
        // variant of each conversation field preferred - GetText reads the database's raw fields, which
        // stay English in every language. A dialogue orb opens its conversation, read by the dialogue
        // screen, and a thought orb runs a splash, so both stay silent here to avoid a double-read. Spoken
        // directly, so it is never subject to the ambient-dialogue setting: a triggered orb is a deliberate
        // interaction, not background chatter.
        public string PostInteractLine()
        {
            if (_orb.HasDialogue || _orb.orbType == OrbType.THOUGHT) return null;
            string text = string.IsNullOrEmpty(_orb.textOverride) ? LocalizedOrbText() : null;
            return TextFilter.Clean(text ?? _orb.GetText());
        }

        private string MorselText()
        {
            if (!_orb.IsMorsel) return null;
            return LocalizedOrbText() ?? _orb.morselText;
        }

        // The orb's clue text in the game's current language, or null to use the database's English
        // field. The game keeps its non-English dialogue data as I2 terms keyed
        // "Conversation/<Articy Id>/<Field>" (loaded only while a non-English language is active), so a
        // missing term simply means English. Field order mirrors GetText: a morsel reads the morsel field
        // (AlternateOrbText) with Description as its fallback - the order the game fills morselText - and
        // a full orb reads Description alone. Fetched unfixed (logical order), the form speech needs.
        private string LocalizedOrbText()
        {
            var conv = _orb.ConversationObject;
            string articyId = conv != null ? Field.LookupValue(conv.fields, "Articy Id") : null;
            if (string.IsNullOrEmpty(articyId)) return null;
            if (_orb.IsMorsel)
                return LocalizedConversationField(articyId, "AlternateOrbText")
                       ?? LocalizedConversationField(articyId, "Description");
            return LocalizedConversationField(articyId, "Description");
        }

        private static string LocalizedConversationField(string articyId, string field)
        {
            string term = "Conversation/" + articyId + "/" + field;
            string s = LocalizationCustomSystem.LocalizationManager.GetLocalizedTerm(term, false, false);
            return string.IsNullOrEmpty(s) || s == term ? null : s;
        }
    }
}
