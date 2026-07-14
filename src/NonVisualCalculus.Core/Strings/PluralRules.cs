using System;

namespace NonVisualCalculus.Core.Strings
{
    /// <summary>
    /// Plural-form selectors for translated count strings: a rule maps a count to the index of the form
    /// to speak, and a table value carries the forms '|'-separated ("{0} hour|{0} hours"). A translation
    /// file names its rule with the "_plural" key; English is the default. The rules are the CLDR
    /// categories in CLDR order, folded to the handful of shapes the game's languages plus Arabic need.
    /// </summary>
    public static class PluralRules
    {
        /// <summary>One form for every count (Chinese, Japanese, Korean, Turkish).</summary>
        public static readonly Func<int, int> One = n => 0;

        /// <summary>Two forms: exactly 1, then everything else (English, German, Spanish, ...).</summary>
        public static readonly Func<int, int> English = n => Math.Abs(n) == 1 ? 0 : 1;

        /// <summary>Two forms: 0 and 1 share the singular, then everything else (French, Portuguese-BR).</summary>
        public static readonly Func<int, int> French = n => Math.Abs(n) <= 1 ? 0 : 1;

        /// <summary>Three forms - one, few, many (Russian, Polish, Ukrainian): 1/21/31 take the first,
        /// 2-4/22-24 the second (except the teens), the rest the third.</summary>
        public static readonly Func<int, int> Slavic = n =>
        {
            int abs = Math.Abs(n);
            int mod10 = abs % 10, mod100 = abs % 100;
            if (mod10 == 1 && mod100 != 11) return 0;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return 1;
            return 2;
        };

        /// <summary>Six forms - zero, one, two, few, many, other (Arabic): 0, 1, 2, then 3-10 and
        /// 11-99 by the last two digits, then the rest.</summary>
        public static readonly Func<int, int> Arabic = n =>
        {
            int abs = Math.Abs(n);
            if (abs == 0) return 0;
            if (abs == 1) return 1;
            if (abs == 2) return 2;
            int mod100 = abs % 100;
            if (mod100 >= 3 && mod100 <= 10) return 3;
            if (mod100 >= 11) return 4;
            return 5;
        };

        /// <summary>The rule a translation file's "_plural" value names, or null when the name is
        /// unknown (the caller keeps English and reports it, so a typo never silently mis-pluralizes).</summary>
        public static Func<int, int>? Resolve(string? name)
        {
            switch (name?.Trim().ToLowerInvariant())
            {
                case "one": return One;
                case "english": return English;
                case "french": return French;
                case "slavic": return Slavic;
                case "arabic": return Arabic;
                default: return null;
            }
        }
    }
}
