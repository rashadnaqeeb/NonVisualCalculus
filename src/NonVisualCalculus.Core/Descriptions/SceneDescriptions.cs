using System;
using System.Collections.Generic;
using System.Numerics;
using NonVisualCalculus.Core.Text;
using NonVisualCalculus.Core.World.Zoning;

namespace NonVisualCalculus.Core.Descriptions
{
    /// <summary>
    /// One scene's authored descriptions, ready to speak: the geometry (anchors and keys) joined to the
    /// prose in the game's language with English as the fallback per key, and the live story state for
    /// variants. Zone text is found by resolving each anchor through the live <see cref="ZoneMap"/> and
    /// taking the one standing in the zone asked about, so nothing stores a zone id. Every spoken framing
    /// word comes from the strings table; the prose is spoken as authored.
    /// </summary>
    public sealed class SceneDescriptions
    {
        public string Scene { get; }
        public SceneGeometry Geometry { get; }
        private readonly SceneProse _prose;
        private readonly SceneProse _english;
        private readonly Func<string, string?> _stateOf;

        public SceneDescriptions(string scene, SceneGeometry geometry, SceneProse prose, SceneProse english,
                                 Func<string, string?> stateOf)
        {
            Scene = scene;
            Geometry = geometry;
            _prose = prose;
            _english = english;
            _stateOf = stateOf;
        }

        /// <summary>Authored text for a prose key: the game-language line, else English, else null.</summary>
        public string? Text(string key)
            => _prose.Get(key, _stateOf) ?? _english.Get(key, _stateOf);

        /// <summary>The anchor standing in <paramref name="zone"/>, nearest to <paramref name="near"/> when
        /// several do (one per zone is the rule; a second marks a distinct part of one zone).</summary>
        public ZoneAnchor? AnchorIn(ZoneMap map, Zone zone, Vector3 near)
        {
            ZoneAnchor? best = null;
            float bestD = float.MaxValue;
            foreach (var a in Geometry.Zones)
            {
                if (!ReferenceEquals(map.ZoneAt(a.Position), zone)) continue;
                float d = Vector3.DistanceSquared(a.Position, near);
                if (best == null || d < bestD) { best = a; bestD = d; }
            }
            return best;
        }

        /// <summary>The zone's authored title, or null when no anchor with a title stands in it.</summary>
        public string? ZoneTitle(ZoneMap map, Zone zone)
        {
            var a = AnchorIn(map, zone, zone.Centroid);
            return a == null ? null : Text("zone." + a.Key + ".title");
        }

        /// <summary>The zone's authored body for the part of it nearest <paramref name="at"/>, or null.</summary>
        public string? ZoneBody(ZoneMap map, Zone zone, Vector3 at)
        {
            var a = AnchorIn(map, zone, at);
            return a == null ? null : Text("zone." + a.Key + ".body");
        }

        public string? DetailTitle(string key) => Text("detail." + key + ".title");
        public string? DetailBody(string key) => Text("detail." + key + ".body");

        /// <summary>Appearance text for an entity, by its exact GameObject name; null when none is authored.</summary>
        public string? ObjectText(string entityName)
        {
            foreach (var o in Geometry.Objects)
                if (string.Equals(o.EntityName, entityName, StringComparison.Ordinal))
                    return Text("object." + o.Key);
            return null;
        }
    }

    /// <summary>The spoken zone lines, composed from authored text and the strings table.</summary>
    public static class ZoneSpeech
    {
        /// <summary>A zone's spoken name: its authored title, else the development fallback of its number
        /// and class word (a shipped scene has a title for every zone).</summary>
        public static string Name(SceneDescriptions? descriptions, ZoneMap map, Zone zone)
        {
            string? title = descriptions?.ZoneTitle(map, zone);
            return !string.IsNullOrEmpty(title) ? title! : Strings.Strings.WorldZoneUnnamed(zone.Id, ClassWord(zone.Class));
        }

        /// <summary>The describe-zone readout: the zone's name, then its body or the nothing-described line.</summary>
        public static string Describe(SceneDescriptions? descriptions, ZoneMap map, Zone zone, Vector3 at)
        {
            string? body = descriptions?.ZoneBody(map, zone, at);
            return SpokenLine.Join(". ", new[]
            {
                Name(descriptions, map, zone),
                string.IsNullOrEmpty(body) ? Strings.Strings.WorldDescribeNone : body,
            });
        }

        public static string ClassWord(ZoneClass cls)
        {
            switch (cls)
            {
                case ZoneClass.Small: return Strings.Strings.WorldZoneClassSmall;
                case ZoneClass.Hall: return Strings.Strings.WorldZoneClassHall;
                case ZoneClass.Passage: return Strings.Strings.WorldZoneClassPassage;
                case ZoneClass.Corridor: return Strings.Strings.WorldZoneClassCorridor;
                case ZoneClass.Stairs: return Strings.Strings.WorldZoneClassStairs;
                default: return Strings.Strings.WorldZoneClassRoom;
            }
        }
    }
}
