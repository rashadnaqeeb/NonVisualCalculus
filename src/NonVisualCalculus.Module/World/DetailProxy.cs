using System;
using NonVisualCalculus.Core.Descriptions;
using NonVisualCalculus.Core.Speech;
using NonVisualCalculus.Core.World;
using Snv = System.Numerics.Vector3;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// A painted detail as a scan item: a point the authored descriptions place on the map (a mural, a
    /// wreck, a shop sign) with no game object behind it. Lists under the details category, so it
    /// cycles, pings and carries a bearing and distance like anything else; its name is the authored
    /// title, and acting on it walks the character there and speaks the authored body (the same text
    /// the describe key gives). Visible and accessible whenever its scene is up: there is no game state
    /// to gate it, and reachability is the navmesh's answer from where the character stands.
    /// </summary>
    internal sealed class DetailProxy : IWalkTarget, IKeyedWorldItem
    {
        // How close the walk must bring the character before the detail counts as reached.
        private const float ArrivalRadius = 2f;

        private readonly DetailPoint _point;
        private readonly Func<SceneDescriptions> _descriptions; // the live set, read at speak time
        private readonly WorldEnvironment _env;
        private readonly SpeechPipeline _speech;

        public DetailProxy(DetailPoint point, Func<SceneDescriptions> descriptions, WorldEnvironment env, SpeechPipeline speech)
        {
            _point = point;
            _descriptions = descriptions;
            _env = env;
            _speech = speech;
        }

        public string Key => _point.Key;
        public string Name => _descriptions()?.DetailTitle(_point.Key) ?? _point.Key;
        public string Body => _descriptions()?.DetailBody(_point.Key);
        public Snv Position => _point.Position;
        public ScanBounds Bounds => ScanBounds.Point(_point.Position);
        public string Category => WorldTaxonomy.Detail;
        public bool IsAccessible => true;
        public bool IsVisible => true;
        public bool IsOpen => false;
        public bool HasPendingDialogue => false;
        public bool RidesPlayer => false;
        public Snv InteractionPoint(Snv from) => _point.Position;
        public ReachState ReachableFrom(Snv from)
            => _env.PathComplete(from, _point.Position) ? ReachState.Reachable : ReachState.Severed;
        public bool ReachIsClickPriced => false;

        /// <summary>Speak the authored body in place (the describe readout); the walk verb reaches here
        /// through <see cref="Interact(bool)"/> once the character arrives.</summary>
        public bool Interact()
        {
            _speech.Speak(Body ?? Core.Strings.Strings.WorldDescribeNone, interrupt: true);
            return true;
        }

        public bool InteractWalks => false;
        public bool Interact(bool run) => Interact();
        public Snv Approach(Snv from, out float heading) { heading = 0f; return _point.Position; }
        public bool WithinInteractionRadius(Snv playerPos) => Snv.Distance(playerPos, _point.Position) <= ArrivalRadius;
        public string PostInteractLine() => null;
    }
}
