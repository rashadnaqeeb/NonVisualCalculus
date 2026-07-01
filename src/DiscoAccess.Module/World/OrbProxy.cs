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
        // The cursor footprint radius is capped here, the orb mirror of an entity's MaxFootprintHalf: a few
        // orbs carry a very large interaction sphere (up to 16 m), and a footprint that wide would read
        // distance-zero from far off and shadow nearby things, so the sensed disc is clamped while the orb's
        // own InteractionRadius still governs the actual trigger range.
        private const float MaxFootprintRadius = 4f;

        private readonly SenseOrb _orb;

        public OrbProxy(SenseOrb orb) { _orb = orb; }

        public string Name => OrbNaming.Resolve(_orb.textOverride, MorselText(), _orb.conversation);
        public Vector3 Position => WorldConvert.ToSnv(_orb.transform.position);

        // The footprint is a circle sized to the orb's interaction radius (capped), not a bare point, so the
        // cursor is "on" the orb anywhere within the disc it can be triggered from - the same real-footprint
        // treatment an entity gets from its renderer bounds, and the fix for an orb whose exact centre sits
        // off the walkable mesh (out over water, a gap) while its interaction reaches walkable ground. The
        // hit test is XZ-only (ObjectCueSystem), so height is folded away here exactly as for an entity.
        public ScanBounds Bounds
            => ScanBounds.Circle(Position, System.Math.Min(_orb.InteractionRadius, MaxFootprintRadius));
        public string Category => WorldTaxonomy.Orb;

        // What the cursor reports: a world-anchored orb whose gameplay conditions are met, or that offers a
        // morsel teaser - the orb-side equivalent of an entity's IsAccessible flag. An orb already triggered
        // is excluded (WasShown): the game's own IsAccessible reflects only prerequisites/skill, never whether
        // the orb has been read, so without this a shown orb stays under the cursor forever - reads its clue
        // on Enter but never leaves, the freshness the sighted player sees fade away. Draw state (whether the
        // orb is currently rendered/orbiting) is deliberately NOT required: the cursor is the blind player's
        // eyes, so an accessible-but-undrawn orb (an orbital orb the character has not walked up to yet, like
        // the halogen-watermark orb read from across the plaza) must still be findable. The party-orbiting
        // thought-cabinet family (afterthought/obsession/paralyzer/thought) rides the character rather than
        // sitting in the world, so it is not a spatial cursor target and is excluded. A DORMANT Modus Mullen
        // orb is likewise excluded (see IsMullenDormant): outside that hidden minigame it is an inactive husk
        // with no clickable orbUI, so Interact could not trigger it and it would sit under the cursor
        // unclearable - but once the minigame activates it (ShowOrbs gives it a live orbUI) it becomes a real
        // target and is findable again.
        public bool IsAccessible
            => IsWorldAnchored && !IsMullenDormant && !_orb.WasShown() && (_orb.IsAccessible || _orb.IsMorsel);

        // A Modus Mullen orb that the minigame has not activated: it carries no orbUI, so the game gives it no
        // click target and our Interact has nothing to Open. Scoped to Mullen orbs - a non-Mullen clue orb
        // deliberately needs no live orbUI to be findable (it gains one when the character walks up to it,
        // which a dormant Mullen orb never does).
        private bool IsMullenDormant => _orb.isMullenOrb && _orb.orbUI == null;
        public bool IsVisible => IsAccessible;

        private bool IsWorldAnchored
            => _orb.orbType == OrbType.MAP || _orb.orbType == OrbType.ORBITAL || _orb.orbType == OrbType.DICK;

        // The orb has no game-authored interaction stand-point; the cursor navigates to the orb body and the
        // walk verb stops within its interaction circle.
        public Vector3 InteractionPoint(Vector3 from) => Position;

        // Orbs are not GameEntity, so they carry no path oracle; reachability is decided by the walk attempt,
        // never pre-judged here - the same way the cursor never pre-rejects an entity on its own oracle.
        public bool IsActionable(Vector3 from) => true;

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
            float dx = playerPos.X - Position.X, dz = playerPos.Z - Position.Z;
            return dx * dx + dz * dz <= _orb.InteractionRadius * _orb.InteractionRadius;
        }

        // Trigger the orb once the character is in range, through the game's own orb click (OrbUiElement.Open):
        // a simple orb floats its text (spoken by PostInteractLine), a dialogue orb opens its conversation (read
        // by the dialogue screen), and both mark it shown and update visuals - which a bare StartConversation
        // would skip, leaving a simple orb's float text unshown. In range an orb is drawn and carries its UI; if
        // it somehow has not (undrawn), fall back to starting the conversation directly so a dialogue orb still
        // reads. Out of range: refuse, so the walk verb reports can't-reach rather than acting from afar.
        public bool Interact()
        {
            Party party = Party.Player;
            Character main = party != null ? party.Main : null;
            if (main == null) return false;
            if (!WithinInteractionRadius(WorldConvert.ToSnv(main.transform.position))) return false;
            var ui = _orb.orbUI;
            if (ui != null) ui.Open();
            else DialogueManager.StartConversation(_orb.conversation);
            return true;
        }

        // What to speak right after triggering. A simple orb floats its clue as a world label (SpawnFloatText)
        // that no dialogue screen or bark reader carries, and that float path cannot be Harmony-hooked (the
        // method is inlined), so the mod voices the text itself here - GetText is exactly what the float shows.
        // A dialogue orb opens its conversation, read by the dialogue screen, and a thought orb runs a splash,
        // so both stay silent here to avoid a double-read. Spoken directly, so it is never subject to the
        // ambient-dialogue setting: a triggered orb is a deliberate interaction, not background chatter.
        public string PostInteractLine()
            => (_orb.HasDialogue || _orb.orbType == OrbType.THOUGHT) ? null : TextFilter.Clean(_orb.GetText());

        private string MorselText() => _orb.IsMorsel ? _orb.morselText : null;
    }
}
