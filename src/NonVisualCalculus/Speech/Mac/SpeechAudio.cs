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

        /// <summary>Length of the ramp that brings a line in from silence.</summary>
        public const double FadeInSeconds = 0.002;

        /// <summary>Length of the ramp that takes a line out to silence.</summary>
        public const double FadeOutSeconds = 0.004;

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

        /// <summary>Ramp the first <see cref="FadeInSeconds"/> of <paramref name="samples"/> up from
        /// zero and the last <see cref="FadeOutSeconds"/> down to zero, in place, on a
        /// raised-cosine curve. AVSpeech can end a line mid-waveform (a plosive such as the end of
        /// "hundred"), and a line that starts or stops on a non-zero sample clicks against the
        /// silence around it. The ramps are too short to hear as a fade. On a line shorter than
        /// both ramps together, each ramp gets its share of the line.</summary>
        public static void FadeEdges(float[] samples, double sampleRate)
        {
            int n = samples.Length;
            if (n == 0)
            {
                return;
            }

            int fadeIn = Math.Max(1, (int)Math.Round(FadeInSeconds * sampleRate));
            int fadeOut = Math.Max(1, (int)Math.Round(FadeOutSeconds * sampleRate));
            if (fadeIn + fadeOut > n)
            {
                fadeIn = Math.Max(1, n * fadeIn / (fadeIn + fadeOut));
                fadeOut = Math.Max(1, n - fadeIn);
            }

            for (int i = 0; i < fadeIn; i++)
            {
                samples[i] *= RaisedCosine((float)i / fadeIn);
            }

            for (int i = 0; i < fadeOut; i++)
            {
                samples[n - 1 - i] *= RaisedCosine((float)i / fadeOut);
            }
        }

        /// <summary>0 at <paramref name="x"/> = 0 rising smoothly to 1 at <paramref name="x"/> = 1.</summary>
        private static float RaisedCosine(float x)
        {
            return 0.5f - 0.5f * (float)Math.Cos(Math.PI * x);
        }

        /// <summary><paramref name="value"/> held to [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
