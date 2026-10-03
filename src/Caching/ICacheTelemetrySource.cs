namespace Caching
{
    /// <summary>
    /// Non-generic view of a cache instance used by the observable gauges.
    /// </summary>
    internal interface ICacheTelemetrySource
    {
        bool TelemetryMetricsEnabled { get; }

        string TelemetryCacheName { get; }

        string TelemetryCacheType { get; }

        long TelemetryEntryCount { get; }

        long TelemetryCapacity { get; }

        long TelemetryEvictCount { get; }

        long TelemetryMemoryUsage { get; }

        long TelemetryMemoryLimit { get; }

        double TelemetryExpirationIntervalSeconds { get; }

        long TelemetryLockWaiting { get; }

        double TelemetryLastSweepSuccessUnixSeconds { get; }
    }
}
