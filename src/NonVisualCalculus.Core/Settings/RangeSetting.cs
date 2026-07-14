using System;

namespace NonVisualCalculus.Core.Settings
{
    /// <summary>How a <see cref="RangeSetting"/>'s value reads in the menu.</summary>
    public enum RangeUnit
    {
        /// <summary>A 0..100 percent scale (a volume), read as "40 percent".</summary>
        Percent,

        /// <summary>A duration in milliseconds, read as "400 milliseconds".</summary>
        Milliseconds,
    }

    /// <summary>
    /// One numeric mod setting on a bounded integer scale - a 0..100 percent volume by default, or any
    /// <see cref="Min"/>..<see cref="Max"/> span with a <see cref="Unit"/> (a millisecond duration) -
    /// adjusted in fixed <see cref="Step"/> increments and persisted through an <see cref="ISettingsStore"/>.
    /// The settings menu steps it with <see cref="Increase"/>/<see cref="Decrease"/> (which report whether
    /// the value actually moved, so the menu can announce a bound); feature code reads
    /// <see cref="Fraction"/> as a 0..1 gain (percent scales only) or <see cref="Value"/> directly.
    /// </summary>
    public sealed class RangeSetting : ModSetting
    {
        private readonly ISettingsStore _store;

        public int DefaultValue { get; }
        public int Step { get; }
        public int Min { get; }
        public int Max { get; }
        public RangeUnit Unit { get; }

        /// <summary>Current value, a whole number in [<see cref="Min"/>, <see cref="Max"/>].</summary>
        public int Value { get; private set; }

        /// <summary>The value as a 0..1 gain, for an audio system to scale by. Percent scales only: a
        /// duration has no gain reading, so asking for one is a caller bug and crashes visibly.</summary>
        public float Fraction => Unit == RangeUnit.Percent
            ? Value / 100f
            : throw new InvalidOperationException("Fraction is meaningless for a " + Unit + " range");

        /// <summary>A 0..100 percent setting (a volume).</summary>
        public RangeSetting(string key, Func<string> label, int defaultValue, int step, ISettingsStore store)
            : this(key, label, defaultValue, step, min: 0, max: 100, RangeUnit.Percent, store)
        {
        }

        public RangeSetting(string key, Func<string> label, int defaultValue, int step, int min, int max,
                            RangeUnit unit, ISettingsStore store)
            : base(key, label)
        {
            Min = min;
            Max = max;
            Unit = unit;
            DefaultValue = Clamp(defaultValue);
            Step = step;
            _store = store;
            Value = Clamp(store.GetInt(key, defaultValue));
        }

        /// <summary>Step up one increment; returns false (and changes nothing) when already at the maximum.</summary>
        public bool Increase() => Adjust(Step);

        /// <summary>Step down one increment; returns false (and changes nothing) when already at the minimum.</summary>
        public bool Decrease() => Adjust(-Step);

        private bool Adjust(int delta)
        {
            int next = Clamp(Value + delta);
            if (next == Value) return false;
            Value = next;
            _store.SetInt(Key, next);
            return true;
        }

        private int Clamp(int v) => v < Min ? Min : v > Max ? Max : v;
    }
}
