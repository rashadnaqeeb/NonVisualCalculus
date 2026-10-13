using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.Strings;

namespace NonVisualCalculus.Core.Descriptions
{
    /// <summary>
    /// The prose half of a scene's description data for one language (descriptions/&lt;scene&gt;.&lt;lang&gt;.txt),
    /// the same key = value format as the lang files. Keys: <c>zone.&lt;key&gt;.title</c>,
    /// <c>zone.&lt;key&gt;.body</c>, <c>detail.&lt;key&gt;.title</c>, <c>detail.&lt;key&gt;.body</c>,
    /// <c>object.&lt;key&gt;</c>. A key may carry story-state variants, one per line, as
    /// <c>key@LOCATION:state</c> (the game's location-state machine name and the state's name); the
    /// variant whose state is live wins over the base line, which is the text for every other state.
    /// </summary>
    public sealed class SceneProse
    {
        public static readonly SceneProse Empty = new SceneProse(new Dictionary<string, string>());

        private readonly Dictionary<string, string> _base = new Dictionary<string, string>();
        // base key -> (location, state, text)
        private readonly Dictionary<string, List<Variant>> _variants = new Dictionary<string, List<Variant>>();

        private struct Variant
        {
            public string Location, State, Text;
        }

        public SceneProse(IReadOnlyDictionary<string, string> entries)
        {
            foreach (var kv in entries)
            {
                int at = kv.Key.IndexOf('@');
                if (at < 0) { _base[kv.Key] = kv.Value; continue; }
                string key = kv.Key.Substring(0, at);
                string cond = kv.Key.Substring(at + 1);
                int colon = cond.IndexOf(':');
                if (colon <= 0 || colon == cond.Length - 1) { _base[kv.Key] = kv.Value; continue; } // malformed: kept verbatim, reported by the validator
                if (!_variants.TryGetValue(key, out var list)) { list = new List<Variant>(); _variants[key] = list; }
                list.Add(new Variant { Location = cond.Substring(0, colon), State = cond.Substring(colon + 1), Text = kv.Value });
            }
        }

        /// <summary>Parse a prose file; errors are the malformed lines.</summary>
        public static SceneProse Parse(string content, out List<string> errors)
            => new SceneProse(Translation.ParseFile(content, out errors));

        /// <summary>Every base key with text (variants fold into their base key).</summary>
        public IEnumerable<string> Keys
        {
            get
            {
                var seen = new HashSet<string>(_base.Keys);
                foreach (var k in _variants.Keys) seen.Add(k);
                return seen;
            }
        }

        /// <summary>Base keys that carry story-state variants, with each variant's location and state.</summary>
        public IEnumerable<KeyValuePair<string, (string location, string state)>> Variants
        {
            get
            {
                foreach (var kv in _variants)
                    foreach (var v in kv.Value)
                        yield return new KeyValuePair<string, (string, string)>(kv.Key, (v.Location, v.State));
            }
        }

        /// <summary>The text for a key under the live story state (<paramref name="stateOf"/> answers a
        /// location's current state, null when unknown), else the base text, else null.</summary>
        public string? Get(string key, Func<string, string?> stateOf)
        {
            if (_variants.TryGetValue(key, out var list))
                foreach (var v in list)
                    if (string.Equals(stateOf(v.Location), v.State, StringComparison.OrdinalIgnoreCase))
                        return v.Text;
            return _base.TryGetValue(key, out string text) ? text : null;
        }

        public bool Has(string key) => _base.ContainsKey(key) || _variants.ContainsKey(key);
    }
}
