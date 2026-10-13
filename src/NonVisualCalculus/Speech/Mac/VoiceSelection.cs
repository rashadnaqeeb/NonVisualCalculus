using System;
using System.Collections.Generic;

namespace NonVisualCalculus.Speech.Mac
{
    /// <summary>
    /// An installed system voice: the identifier both macOS synthesizers use to select it, plus
    /// its display name and BCP 47 language.
    /// </summary>
    public sealed class VoiceInfo
    {
        public VoiceInfo(string identifier, string name, string language)
        {
            Identifier = identifier;
            Name = name;
            Language = language;
        }

        public readonly string Identifier;
        public readonly string Name;
        public readonly string Language;
    }

    /// <summary>
    /// The pure logic behind the Voice setting: how a chosen voice is stored and how the voice
    /// list is filtered to the game's language. No Unity or Objective-C dependencies, so the test
    /// project compiles this file directly.
    /// </summary>
    public static class VoiceSelection
    {
        /// <summary>
        /// The voice setting's value while the player has not picked a voice: the Spoken Content
        /// voice is used, and followed when it changes.
        /// </summary>
        public const string DefaultKey = "";

        private const char KeySeparator = '|';

        /// <summary>Primary language subtag of a voice language tag ("en-US", "zh_CN"),
        /// lowercased. Null or empty gives "".</summary>
        public static string PrimaryLanguage(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return "";
            }

            int cut = tag.IndexOfAny(new[] { '-', '_' });
            return (cut > 0 ? tag.Substring(0, cut) : tag).ToLowerInvariant();
        }

        /// <summary>Primary subtag for the game's language code (<c>CurrentLanguage.LanguageCode</c>:
        /// "en", "pt-BR", "zh-CN"; the mod's .po files are named after the same values), or ""
        /// when there is none. Songs of Conquest already speaks BCP 47, so this is the voice tag
        /// rule applied to the game's own code; Endless Space 2's copy of this file maps Steam's
        /// language names here instead.</summary>
        public static string GamePrimaryLanguage(string gameLanguage)
        {
            return PrimaryLanguage(gameLanguage);
        }

        /// <summary>
        /// The setting value for a voice: its language and name, which is what the player sees in
        /// the picker. macOS ships one name (Eddy, Flo, Grandma, ...) in a dozen languages, so the
        /// name alone would be ambiguous.
        /// </summary>
        public static string MakeKey(VoiceInfo voice)
        {
            return voice.Language + KeySeparator + voice.Name;
        }

        /// <summary>
        /// The voice a stored key names, or null when it is not installed. A key with an empty
        /// language part takes the first voice of that name.
        /// </summary>
        public static VoiceInfo FindByKey(IList<VoiceInfo> voices, string key)
        {
            int cut = key.IndexOf(KeySeparator);
            if (cut < 0)
            {
                return null;
            }

            string language = key.Substring(0, cut);
            string name = key.Substring(cut + 1);
            for (int i = 0; i < voices.Count; i++)
            {
                VoiceInfo voice = voices[i];
                if (voice.Name != name)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(language) || voice.Language == language)
                {
                    return voice;
                }
            }

            return null;
        }

        /// <summary>
        /// The voices whose primary language subtag is <paramref name="primaryLanguage"/> ("en"
        /// takes en-US, en-GB and en-AU alike). Every voice when the subtag is empty or nothing
        /// matches: a picker with no rows would be a dead end.
        /// </summary>
        public static List<VoiceInfo> ForLanguage(IList<VoiceInfo> voices, string primaryLanguage)
        {
            // The empty-subtag guard is load-bearing: a voice with no language would otherwise
            // match "".
            List<VoiceInfo> matches = new List<VoiceInfo>();
            if (primaryLanguage.Length > 0)
            {
                for (int i = 0; i < voices.Count; i++)
                {
                    if (PrimaryLanguage(voices[i].Language) == primaryLanguage)
                    {
                        matches.Add(voices[i]);
                    }
                }
            }

            if (matches.Count > 0)
            {
                return matches;
            }

            return new List<VoiceInfo>(voices);
        }

        /// <summary>
        /// Give voices that share a name and language distinct names ("Foo", "Foo (2)",
        /// "Foo (3)"), so every row has its own key and label. The order the voices arrive in is
        /// kept.
        /// </summary>
        public static List<VoiceInfo> Disambiguate(IList<VoiceInfo> voices)
        {
            Dictionary<string, int> seen = new Dictionary<string, int>();
            List<VoiceInfo> result = new List<VoiceInfo>(voices.Count);
            for (int i = 0; i < voices.Count; i++)
            {
                VoiceInfo voice = voices[i];
                string key = MakeKey(voice);
                int count;
                seen[key] = seen.TryGetValue(key, out count) ? count + 1 : 1;
                result.Add(
                    seen[key] == 1
                        ? voice
                        : new VoiceInfo(
                            voice.Identifier,
                            voice.Name + " (" + seen[key] + ")",
                            voice.Language
                        )
                );
            }

            return result;
        }
    }
}
