using System.Collections.Generic;
using System.Text;
using DiscoAccess.Core.Text;

namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>
    /// Type-ahead search engine - ported, with permission, from OniAccess (VisionNotIncluded) via
    /// wotr-access; adapted to DiscoAccess (speech + key routing live in <see cref="TraditionalNavigator"/>,
    /// which feeds typed characters and result steps in; this class is pure matching/result state, so it
    /// is unit-testable off-engine).
    ///
    /// Builds a filtered results list over a flat item list with TIERED matching: start-of-string
    /// whole word, start-of-string prefix, mid-string whole word, mid-string word prefix, substring
    /// anywhere, then space-delimited word-prefix abbreviation ("ga pi" matches "gas pipe"). Within a
    /// tier, shorter names rank first (match position breaks ties), and matches in the item's NAME
    /// (before the first comma) rank ahead of matches in its appended metadata - which fits our focus
    /// text "label, role, value", so a name match outranks one in the role/value tail. Diacritics are
    /// ignored. Typing the same letter repeatedly cycles the starts-with results.
    /// </summary>
    public class TypeAheadSearch
    {
        private readonly StringBuilder _buffer = new StringBuilder(32);

        private bool _isSearchActive;
        private List<int> _resultIndices = new List<int>();
        private List<string> _resultNames = new List<string>();
        private int _resultCursor;

        // Working lists for search, one set per match tier (avoids allocation).
        private const int TierCount = 6;
        private readonly List<int>[] _tierIndices;
        private readonly List<string>[] _tierNames;
        private readonly List<int>[] _tierPositions;
        private readonly List<int>[] _tierSortLengths;
        private readonly List<int>[] _tierInSegment;
        private List<int> _workIndices = new List<int>();
        private List<string> _workNames = new List<string>();

        // Announce-and-move callback (called with the ORIGINAL item index).
        private System.Action<int>? _announceResult;
        // Spoken when the buffer matches nothing (gets the buffer text).
        public System.Action<string>? OnNoMatch { get; set; }

        public TypeAheadSearch()
        {
            _tierIndices = new List<int>[TierCount];
            _tierNames = new List<string>[TierCount];
            _tierPositions = new List<int>[TierCount];
            _tierSortLengths = new List<int>[TierCount];
            _tierInSegment = new List<int>[TierCount];
            for (int t = 0; t < TierCount; t++)
            {
                _tierIndices[t] = new List<int>();
                _tierNames[t] = new List<string>();
                _tierPositions[t] = new List<int>();
                _tierSortLengths[t] = new List<int>();
                _tierInSegment[t] = new List<int>();
            }
        }

        public string Buffer => _buffer.ToString();
        public bool HasBuffer => _buffer.Length > 0;
        public bool IsSearchActive => _isSearchActive;
        public int ResultCount => _resultIndices.Count;

        public void AddChar(char c) => _buffer.Append(c);

        public bool RemoveChar()
        {
            if (_buffer.Length == 0) return false;
            _buffer.Length--;
            return true;
        }

        public void Clear()
        {
            _buffer.Length = 0;
            _isSearchActive = false;
            _resultIndices.Clear();
            _resultNames.Clear();
            _resultCursor = 0;
            _announceResult = null;
        }

        /// <summary>Run the tiered search over the items and move/announce the best result.</summary>
        public void Search(int itemCount, System.Func<int, string> nameByIndex, System.Action<int> announceResult)
        {
            // Repeat single-letter: typing the same letter again cycles the starts-with results
            // (b -> Beaver, b -> Bat, b -> Brewery).
            string bufferStr = _buffer.ToString();
            if (_isSearchActive && _resultIndices.Count > 0 && _buffer.Length > 1 && IsAllSameChar(bufferStr))
            {
                _buffer.Length = 1;
                if (announceResult != null) _announceResult = announceResult;
                CycleStartsWithResults();
                return;
            }

            if (announceResult != null) _announceResult = announceResult;

            string trimmed = bufferStr.TrimEnd();
            if (!HasBuffer || itemCount == 0 || trimmed.Length == 0)
            {
                _resultIndices.Clear();
                _resultNames.Clear();
                _resultCursor = 0;
                _isSearchActive = true;
                OnNoMatch?.Invoke(bufferStr);
                return;
            }

            for (int t = 0; t < TierCount; t++)
            {
                _tierIndices[t].Clear();
                _tierNames[t].Clear();
                _tierPositions[t].Clear();
                _tierSortLengths[t].Clear();
                _tierInSegment[t].Clear();
            }
            string lowerBuffer = trimmed.ToLowerInvariant();

            for (int i = 0; i < itemCount; i++)
            {
                string name = nameByIndex(i);
                if (string.IsNullOrEmpty(name)) continue;
                int tier = MatchTier(name.ToLowerInvariant(), lowerBuffer, out int pos);
                if (tier >= 0)
                {
                    _tierIndices[tier].Add(i);
                    _tierNames[tier].Add(name);
                    _tierPositions[tier].Add(pos);
                    // Matches inside the name (before the first comma) rank ahead of matches inside the
                    // appended metadata; sort length is likewise name-only.
                    int comma = name.IndexOf(',');
                    int nameLen = comma >= 0 ? comma : name.Length;
                    _tierSortLengths[tier].Add(nameLen);
                    _tierInSegment[tier].Add(pos < nameLen ? 0 : 1);
                }
            }

            for (int t = 0; t < TierCount; t++)
                if (_tierIndices[t].Count > 1)
                    SortByLength(_tierIndices[t], _tierNames[t], _tierPositions[t], _tierSortLengths[t], _tierInSegment[t]);

            // Merge: name (pre-comma) matches across all tiers before metadata (post-comma) matches.
            _workIndices.Clear();
            _workNames.Clear();
            for (int inSeg = 0; inSeg <= 1; inSeg++)
                for (int t = 0; t < TierCount; t++)
                    for (int i = 0; i < _tierIndices[t].Count; i++)
                        if (_tierInSegment[t][i] == inSeg)
                        {
                            _workIndices.Add(_tierIndices[t][i]);
                            _workNames.Add(_tierNames[t][i]);
                        }

            if (_workIndices.Count == 0)
            {
                _resultIndices.Clear();
                _resultNames.Clear();
                _resultCursor = 0;
                _isSearchActive = true;
                OnNoMatch?.Invoke(bufferStr);
            }
            else
            {
                var tempIndices = _resultIndices;
                var tempNames = _resultNames;
                _resultIndices = _workIndices;
                _resultNames = _workNames;
                _workIndices = tempIndices;
                _workNames = tempNames;
                _resultCursor = 0;
                _isSearchActive = true;
                AnnounceCurrentResult();
            }
        }

        // Cycle forward within starts-with results only, so single-letter repeat doesn't wrap into
        // mid-string or substring matches.
        private void CycleStartsWithResults()
        {
            if (_resultIndices.Count == 0) return;
            char letter = char.ToLowerInvariant(_buffer[0]);
            int count = 0;
            for (int i = 0; i < _resultNames.Count; i++)
            {
                if (_resultNames[i].Length > 0 && char.ToLowerInvariant(_resultNames[i][0]) == letter) count++;
                else break;
            }
            if (count == 0) return;
            _resultCursor = (_resultCursor + 1) % count;
            AnnounceCurrentResult();
        }

        /// <summary>Step within the filtered results (wrapping). +1 next, -1 previous.</summary>
        public void NavigateResults(int direction)
        {
            if (_resultIndices.Count == 0) return;
            int count = _resultIndices.Count;
            _resultCursor = ((_resultCursor + direction) % count + count) % count;
            AnnounceCurrentResult();
        }

        public void JumpToFirstResult()
        {
            if (_resultIndices.Count == 0) return;
            _resultCursor = 0;
            AnnounceCurrentResult();
        }

        public void JumpToLastResult()
        {
            if (_resultIndices.Count == 0) return;
            _resultCursor = _resultIndices.Count - 1;
            AnnounceCurrentResult();
        }

        private void AnnounceCurrentResult()
        {
            if (_resultIndices.Count == 0) return;
            _announceResult?.Invoke(_resultIndices[_resultCursor]);
        }

        private static bool IsAllSameChar(string s)
        {
            char first = s[0];
            for (int i = 1; i < s.Length; i++)
                if (s[i] != first) return false;
            return true;
        }

        // Insertion-sort parallel lists by sort length ascending, position as tiebreaker (stable).
        private static void SortByLength(List<int> indices, List<string> names, List<int> positions,
            List<int> sortLengths, List<int> inSegment)
        {
            for (int i = 1; i < positions.Count; i++)
            {
                int pos = positions[i];
                int idx = indices[i];
                string name = names[i];
                int len = sortLengths[i];
                int seg = inSegment[i];
                int j = i - 1;
                while (j >= 0 && (sortLengths[j] > len || (sortLengths[j] == len && positions[j] > pos)))
                {
                    positions[j + 1] = positions[j];
                    indices[j + 1] = indices[j];
                    names[j + 1] = names[j];
                    sortLengths[j + 1] = sortLengths[j];
                    inSegment[j + 1] = inSegment[j];
                    j--;
                }
                positions[j + 1] = pos;
                indices[j + 1] = idx;
                names[j + 1] = name;
                sortLengths[j + 1] = len;
                inSegment[j + 1] = seg;
            }
        }

        /// <summary>Match tier for a prefix against a name (both lowercase), or -1. 0 = start whole
        /// word, 1 = start prefix, 2 = mid whole word, 3 = mid word prefix, 4 = substring anywhere,
        /// 5 = space-delimited word-prefix abbreviation ("ga pi" in "gas pipe").</summary>
        internal static int MatchTier(string lowerName, string lowerPrefix, out int position)
        {
            position = -1;
            lowerName = TextUtil.RemoveDiacritics(lowerName);
            lowerPrefix = TextUtil.RemoveDiacritics(lowerPrefix);
            int prefixLen = lowerPrefix.Length;
            if (prefixLen > lowerName.Length) return -1;

            if (string.Compare(lowerName, 0, lowerPrefix, 0, prefixLen, System.StringComparison.Ordinal) == 0)
            {
                position = 0;
                bool wholeWord = lowerName.Length == prefixLen || lowerName[prefixLen] == ' ' || lowerName[prefixLen] == ',';
                return wholeWord ? 0 : 1;
            }

            for (int i = 1; i < lowerName.Length; i++)
            {
                char prev = lowerName[i - 1];
                if (prev != ' ' && prev != ',') continue;
                if (lowerName[i] == ' ') continue;
                if (lowerName.Length - i < prefixLen) break;
                if (string.Compare(lowerName, i, lowerPrefix, 0, prefixLen, System.StringComparison.Ordinal) == 0)
                {
                    int afterMatch = i + prefixLen;
                    bool wholeWord = afterMatch >= lowerName.Length || lowerName[afterMatch] == ' ' || lowerName[afterMatch] == ',';
                    position = i;
                    return wholeWord ? 2 : 3;
                }
            }

            int idx = lowerName.IndexOf(lowerPrefix, System.StringComparison.Ordinal);
            if (idx >= 0)
            {
                position = idx;
                return 4;
            }

            if (lowerPrefix.IndexOf(' ') >= 0)
            {
                int abbrevPos = MatchWordPrefixTokens(lowerName, lowerPrefix);
                if (abbrevPos >= 0)
                {
                    position = abbrevPos;
                    return 5;
                }
            }

            return -1;
        }

        // Position of the first matched word if every space-delimited token in the prefix is a prefix
        // of a distinct word in the name, consumed in order, all within one comma-delimited segment.
        private static int MatchWordPrefixTokens(string lowerName, string lowerPrefix)
        {
            string[] rawTokens = lowerPrefix.Split(' ');
            int tokenCount = 0;
            for (int t = 0; t < rawTokens.Length; t++)
                if (rawTokens[t].Length > 0) rawTokens[tokenCount++] = rawTokens[t];
            if (tokenCount == 0) return -1;

            int tokenIdx = 0;
            int firstPos = -1;
            int i = 0;
            while (i < lowerName.Length)
            {
                char c = lowerName[i];
                if (c == ',')
                {
                    tokenIdx = 0;
                    firstPos = -1;
                    i++;
                    continue;
                }
                if (c == ' ') { i++; continue; }

                if (tokenIdx < tokenCount)
                {
                    string token = rawTokens[tokenIdx];
                    bool fits = i + token.Length <= lowerName.Length
                        && string.Compare(lowerName, i, token, 0, token.Length, System.StringComparison.Ordinal) == 0;
                    if (fits)
                    {
                        if (tokenIdx == 0) firstPos = i;
                        tokenIdx++;
                        if (tokenIdx == tokenCount) return firstPos;
                        i += token.Length;
                    }
                }
                while (i < lowerName.Length && lowerName[i] != ' ' && lowerName[i] != ',') i++;
            }

            return -1;
        }
    }
}
