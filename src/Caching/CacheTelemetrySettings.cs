namespace Caching
{
    /// <summary>
    /// Per-cache telemetry settings.
    /// Telemetry is emitted through the BCL Meter and ActivitySource named in CacheTelemetryNames and costs
    /// effectively nothing until a collector subscribes. These settings are read on every operation and may be changed at any time.
    /// </summary>
    public class CacheTelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch for this cache instance. When false, the instance emits no metrics and no spans.
        /// Default is true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Emit metrics for this cache instance. Default is true.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Emit spans for this cache instance. Default is true.
        /// </summary>
        public bool EnableTraces { get; set; } = true;

        /// <summary>
        /// Emit spans for pure in-memory lookups (Get, GetOrDefault, TryGet, Contains).
        /// Lookups are always measured by metrics; spans on them are high volume and usually add noise rather than insight.
        /// Default is false.
        /// </summary>
        public bool TraceLookups { get; set; } = false;

        /// <summary>
        /// Record exception messages on spans. Exception messages from persistence drivers or value factories may contain keys,
        /// file paths, or other data that should not leave the process, so only the exception type is recorded by default.
        /// Default is false.
        /// </summary>
        public bool RecordExceptionMessages { get; set; } = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CacheTelemetrySettings()
        {
        }

        #endregion
    }
}
