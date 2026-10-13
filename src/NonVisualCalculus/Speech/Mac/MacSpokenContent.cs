using System;
using System.Globalization;

namespace NonVisualCalculus.Speech.Mac
{
    /// <summary>
    /// The voice and rate the player set under System Settings, Accessibility, Spoken Content on
    /// macOS. The mod speaks with exactly these, which makes Spoken Content its voice picker:
    /// there is nothing to configure in the mod. The voice comes from
    /// NSSpeechSynthesizer.defaultVoice, public API. The rate has no public accessor: it lives in
    /// the com.apple.Accessibility preference domain, readable through NSUserDefaults without any
    /// file or privacy permission, under a key Apple does not document. Only called on macOS.
    /// </summary>
    public static class MacSpokenContent
    {
        /// <summary>The identifier of the Spoken Content voice, which NSSpeechSynthesizer and
        /// AVSpeechSynthesisVoice share, or null when macOS reports none.</summary>
        public static string DefaultVoiceIdentifier()
        {
            try
            {
                ObjC.LoadSpeechFrameworks();
                string id = ObjC.ToManagedString(
                    ObjC.Send(ObjC.Class("NSSpeechSynthesizer"), ObjC.Sel("defaultVoice"))
                );
                if (string.IsNullOrEmpty(id))
                {
                    Log.Info("speech: macOS reports no default speech voice");
                    return null;
                }

                return id;
            }
            catch (Exception e)
            {
                Log.Error("speech: macOS default voice lookup failed: " + e);
                return null;
            }
        }

        /// <summary>
        /// The Spoken Content rate for the voice with <paramref name="identifier"/>, on AVSpeech's
        /// own [0, 1] scale, or -1 when the preference is absent. Read from the
        /// com.apple.Accessibility domain, key SpokenContentDefaultVoiceSelectionsByLanguage: an
        /// array alternating a language code with a dictionary holding voiceId and rate.
        /// </summary>
        public static float DefaultVoiceRate(string identifier)
        {
            try
            {
                ObjC.LoadSpeechFrameworks();
                // alloc/init hands back a +1 reference, released below. objectForKey: returns an
                // object the defaults keep alive for this pool cycle.
                IntPtr defaults = ObjC.Send(
                    ObjC.Send(ObjC.Class("NSUserDefaults"), ObjC.Sel("alloc")),
                    ObjC.Sel("initWithSuiteName:"),
                    ObjC.NSString("com.apple.Accessibility")
                );
                IntPtr selections = ObjC.Send(
                    defaults,
                    ObjC.Sel("objectForKey:"),
                    ObjC.NSString("SpokenContentDefaultVoiceSelectionsByLanguage")
                );
                ObjC.Release(defaults);
                if (!ObjC.IsKindOf(selections, "NSArray"))
                {
                    Log.Info("speech: macOS has no Spoken Content voice selections; keeping the default rate");
                    return -1f;
                }

                long count = ObjC.Count(selections);
                for (long i = 0; i < count; i++)
                {
                    IntPtr entry = ObjC.Send(selections, ObjC.Sel("objectAtIndex:"), new IntPtr(i));
                    if (!ObjC.IsKindOf(entry, "NSDictionary"))
                    {
                        continue;
                    }

                    string voiceId = ObjC.ToManagedString(
                        ObjC.Send(entry, ObjC.Sel("objectForKey:"), ObjC.NSString("voiceId"))
                    );
                    if (voiceId != identifier)
                    {
                        continue;
                    }

                    string rateText = ObjC.ToManagedString(
                        ObjC.Send(entry, ObjC.Sel("objectForKey:"), ObjC.NSString("rate"))
                    );
                    float rate = ParseRate(rateText);
                    if (rate < 0f)
                    {
                        Log.Error("speech: Spoken Content rate for " + identifier + " is unreadable: \"" + rateText + "\"");
                    }

                    return rate;
                }

                Log.Info("speech: no Spoken Content rate stored for " + identifier + "; keeping the default rate");
                return -1f;
            }
            catch (Exception e)
            {
                Log.Error("speech: macOS Spoken Content rate lookup failed: " + e);
                return -1f;
            }
        }

        /// <summary>Parse a rate as macOS stores it ("0.9"), always with a period regardless of
        /// the game's locale. -1 when the text is not a number in [0, 1].</summary>
        public static float ParseRate(string text)
        {
            float rate;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out rate))
            {
                return -1f;
            }

            return rate >= 0f && rate <= 1f ? rate : -1f;
        }
    }
}
