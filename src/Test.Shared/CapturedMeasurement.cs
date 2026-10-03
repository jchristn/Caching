namespace Test.Shared
{
    using System.Collections.Generic;

    /// <summary>
    /// One measurement captured by TelemetryCapture.
    /// </summary>
    public sealed class CapturedMeasurement
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="unit">Instrument unit.</param>
        /// <param name="value">Value.</param>
        /// <param name="tags">Tags.</param>
        public CapturedMeasurement(string instrument, string unit, double value, Dictionary<string, string> tags)
        {
            Instrument = instrument;
            Unit = unit;
            Value = value;
            Tags = tags;
        }

        /// <summary>
        /// Instrument name.
        /// </summary>
        public string Instrument { get; }

        /// <summary>
        /// Instrument unit.
        /// </summary>
        public string Unit { get; }

        /// <summary>
        /// Value.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; }

        /// <summary>
        /// True when every alternating key/value filter matches a tag. A null value means the tag must be absent.
        /// </summary>
        /// <param name="tagFilters">Alternating tag key and value pairs.</param>
        /// <returns>True if all filters match.</returns>
        public bool Matches(string[] tagFilters)
        {
            if (tagFilters == null) return true;

            for (int i = 0; i + 1 < tagFilters.Length; i += 2)
            {
                bool present = Tags.TryGetValue(tagFilters[i], out string actual);
                if (tagFilters[i + 1] == null)
                {
                    if (present) return false;
                }
                else if (!present || actual != tagFilters[i + 1])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
