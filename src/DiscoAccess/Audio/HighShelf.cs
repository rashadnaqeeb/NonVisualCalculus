using System;

namespace DiscoAccess.Audio
{
    /// <summary>
    /// A high-shelf biquad (RBJ Audio EQ Cookbook, shelf slope 1): unity below the corner, a flat
    /// <c>gainDb</c> above it. Hand-rolled because NAudio's <c>BiQuadFilter</c> only exposes a state-
    /// preserving retune for lowpass/highpass/peaking - its high shelf is a factory, and a fresh instance
    /// resets the delay state, which clicks when the shelf ramps mid-signal. <see cref="Set"/> here swaps
    /// coefficients only, so the rear cue can retune every block while a cue plays. At 0 dB the
    /// coefficients reduce exactly to identity, so a transparent shelf can stay in the signal path.
    /// Audio-thread only, like the voice that owns it.
    /// </summary>
    internal sealed class HighShelf
    {
        // Coefficients normalized by a0.
        private double _b0, _b1, _b2, _a1, _a2;
        // Direct form 1 state.
        private float _x1, _x2, _y1, _y2;

        public void Set(float sampleRate, float cornerHz, float gainDb)
        {
            double a = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * cornerHz / sampleRate;
            double cosW0 = Math.Cos(w0);
            double alpha = Math.Sin(w0) / 2.0 * Math.Sqrt(2.0); // shelf slope 1
            double k = 2.0 * Math.Sqrt(a) * alpha;

            double b0 = a * ((a + 1.0) + (a - 1.0) * cosW0 + k);
            double b1 = -2.0 * a * ((a - 1.0) + (a + 1.0) * cosW0);
            double b2 = a * ((a + 1.0) + (a - 1.0) * cosW0 - k);
            double a0 = (a + 1.0) - (a - 1.0) * cosW0 + k;
            double a1 = 2.0 * ((a - 1.0) - (a + 1.0) * cosW0);
            double a2 = (a + 1.0) - (a - 1.0) * cosW0 - k;

            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }

        public float Transform(float x)
        {
            double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = (float)y;
            return (float)y;
        }
    }
}
