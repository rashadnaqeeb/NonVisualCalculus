using System;

namespace NonVisualCalculus.Speech.Mac
{
    /// <summary>
    /// The sample arithmetic of the streamed speech queue, kept pure so it can be tested off the
    /// engine: what of a rendered utterance is speech, and what is the silence the renderer
    /// wraps around it.
    /// </summary>
    public static class SpeechAudio
    {
        /// <summary>Samples quieter than this are silence.</summary>
        public const float SilenceThreshold = 0.004f;

        /// <summary>Seconds kept on each side of the trimmed speech, so a consonant is not clipped.</summary>
        public const double KeepEdgeSeconds = 0.005;

        /// <summary>The speech between the first and last samples above the silence threshold,
        /// plus <see cref="KeepEdgeSeconds"/> either side. Empty when nothing in the first
        /// <paramref name="count"/> samples is above the threshold.</summary>
        public static float[] Trim(float[] samples, int count, double sampleRate)
        {
            int start = 0;
            while (start < count && Math.Abs(samples[start]) < SilenceThreshold)
            {
                start++;
            }

            if (start == count)
            {
                return new float[0];
            }

            int end = count - 1;
            while (end > start && Math.Abs(samples[end]) < SilenceThreshold)
            {
                end--;
            }

            int keep = (int)(KeepEdgeSeconds * sampleRate);
            start = Math.Max(0, start - keep);
            end = Math.Min(count - 1, end + keep);
            float[] result = new float[end - start + 1];
            Array.Copy(samples, start, result, 0, result.Length);
            return result;
        }

        /// <summary>Multiply <paramref name="samples"/> by <paramref name="gain"/> in place, holding
        /// the result to full scale: a gain above 1 makes the voice louder than it renders itself,
        /// and clips whatever peaks that pushes past [-1, 1].</summary>
        public static void Scale(float[] samples, float gain)
        {
            if (gain == 1f)
            {
                return;
            }

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Clamp(samples[i] * gain, -1f, 1f);
            }
        }

        /// <summary><paramref name="value"/> held to [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
