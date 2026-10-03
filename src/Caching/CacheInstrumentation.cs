namespace Caching
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;

    /// <summary>
    /// Process-wide Meter, ActivitySource, and instruments for the Caching library.
    /// Instruments are created once. Observable gauges enumerate live cache instances through a weak registry
    /// so an undisposed cache never leaks because of telemetry.
    /// </summary>
    internal static class CacheInstrumentation
    {
        #region Internal-Members

        // Declared first: static field initializers run in textual order and the histograms below read it.
        internal static readonly double[] DurationBuckets = new double[]
        {
            0.000005, 0.00001, 0.000025, 0.00005, 0.0001, 0.00025, 0.0005,
            0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10
        };

        internal static readonly string Version = ResolveVersion();

        internal static readonly ActivitySource Source = new ActivitySource(CacheTelemetryNames.ActivitySourceName, Version);

        internal static readonly Meter Meter = new Meter(CacheTelemetryNames.MeterName, Version);

        internal static readonly Histogram<double> OperationDuration = CreateDurationHistogram(
            CacheTelemetryNames.OperationDuration,
            "Duration of a public cache operation.");

        internal static readonly Counter<long> Lookups = Meter.CreateCounter<long>(
            CacheTelemetryNames.Lookups,
            "{lookup}",
            "Cache lookups by result (hit or miss).");

        internal static readonly Counter<long> Evictions = Meter.CreateCounter<long>(
            CacheTelemetryNames.Evictions,
            "{entry}",
            "Entries evicted by reason (capacity or memory).");

        internal static readonly Counter<long> Expirations = Meter.CreateCounter<long>(
            CacheTelemetryNames.Expirations,
            "{entry}",
            "Entries removed by the background expiration task.");

        internal static readonly Counter<long> Prepopulated = Meter.CreateCounter<long>(
            CacheTelemetryNames.Prepopulated,
            "{entry}",
            "Entries loaded from the persistence driver by Prepopulate.");

        internal static readonly Counter<long> Errors = Meter.CreateCounter<long>(
            CacheTelemetryNames.Errors,
            "{error}",
            "Failures by component and error type.");

        internal static readonly Histogram<double> PersistenceDuration = CreateDurationHistogram(
            CacheTelemetryNames.PersistenceDuration,
            "Duration of a call into the persistence driver.");

        internal static readonly Counter<long> PersistenceCalls = Meter.CreateCounter<long>(
            CacheTelemetryNames.PersistenceCalls,
            "{call}",
            "Calls into the persistence driver by operation and outcome.");

        internal static readonly Histogram<double> ExpirationSweepDuration = CreateDurationHistogram(
            CacheTelemetryNames.ExpirationSweepDuration,
            "Duration of one background expiration sweep.");

        internal static readonly Histogram<double> LockWaitDuration = CreateDurationHistogram(
            CacheTelemetryNames.LockWaitDuration,
            "Time spent waiting for the atomic lock used by GetOrAdd and AddOrUpdate.");

        #endregion

        #region Private-Members

        private static readonly object _RegistryLock = new object();
        private static readonly List<WeakReference<ICacheTelemetrySource>> _Registry = new List<WeakReference<ICacheTelemetrySource>>();

        private static readonly Dictionary<string, string> _OperationSpanNames = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _PersistenceSpanNames = new Dictionary<string, string>();

        #endregion

        #region Constructors-and-Factories

        static CacheInstrumentation()
        {
            foreach (string op in new string[]
            {
                CacheTelemetryNames.OperationGet,
                CacheTelemetryNames.OperationGetOrDefault,
                CacheTelemetryNames.OperationTryGet,
                CacheTelemetryNames.OperationContains,
                CacheTelemetryNames.OperationAddReplace,
                CacheTelemetryNames.OperationGetOrAdd,
                CacheTelemetryNames.OperationAddOrUpdate,
                CacheTelemetryNames.OperationRemove,
                CacheTelemetryNames.OperationTryRemove,
                CacheTelemetryNames.OperationClear,
                CacheTelemetryNames.OperationPrepopulate
            })
            {
                _OperationSpanNames[op] = CacheTelemetryNames.SpanOperationPrefix + op;
            }

            foreach (string op in new string[]
            {
                CacheTelemetryNames.PersistenceWrite,
                CacheTelemetryNames.PersistenceDelete,
                CacheTelemetryNames.PersistenceClear,
                CacheTelemetryNames.PersistenceGet,
                CacheTelemetryNames.PersistenceEnumerate
            })
            {
                _PersistenceSpanNames[op] = CacheTelemetryNames.SpanPersistencePrefix + op;
            }

            Meter.CreateObservableGauge<long>(
                CacheTelemetryNames.Entries,
                () => ObserveLong(s => s.TelemetryEntryCount),
                "{entry}",
                "Current number of entries.");

            Meter.CreateObservableGauge<long>(
                CacheTelemetryNames.Capacity,
                () => ObserveLong(s => s.TelemetryCapacity),
                "{entry}",
                "Configured maximum number of entries.");

            Meter.CreateObservableGauge<long>(
                CacheTelemetryNames.EvictCount,
                () => ObserveLong(s => s.TelemetryEvictCount),
                "{entry}",
                "Configured number of entries evicted when capacity is reached.");

            Meter.CreateObservableGauge<long>(
                CacheTelemetryNames.MemoryUsage,
                () => ObserveLong(s => s.TelemetryMemoryUsage),
                "By",
                "Current estimated memory usage.");

            Meter.CreateObservableGauge<long>(
                CacheTelemetryNames.MemoryLimit,
                () => ObserveLong(s => s.TelemetryMemoryLimit),
                "By",
                "Configured memory limit. Zero means no limit.");

            Meter.CreateObservableGauge<double>(
                CacheTelemetryNames.ExpirationInterval,
                () => ObserveDouble(s => s.TelemetryExpirationIntervalSeconds, false),
                "s",
                "Configured interval between expiration sweeps.");

            Meter.CreateObservableGauge<long>(
                CacheTelemetryNames.LockWaiting,
                () => ObserveLong(s => s.TelemetryLockWaiting),
                "{operation}",
                "Operations currently waiting for the atomic lock.");

            Meter.CreateObservableGauge<double>(
                CacheTelemetryNames.ExpirationSweepLastSuccess,
                () => ObserveDouble(s => s.TelemetryLastSweepSuccessUnixSeconds, true),
                "s",
                "Unix time of the last expiration sweep that completed successfully.");

            Meter.CreateObservableGauge<long>(
                CacheTelemetryNames.BuildInfo,
                ObserveBuildInfo,
                "{cache}",
                "Always 1 per live cache instance, labeled with the library version.");
        }

        #endregion

        #region Internal-Methods

        internal static void Register(ICacheTelemetrySource source)
        {
            if (source == null) return;

            lock (_RegistryLock)
            {
                _Registry.Add(new WeakReference<ICacheTelemetrySource>(source));
            }
        }

        internal static void Unregister(ICacheTelemetrySource source)
        {
            if (source == null) return;

            lock (_RegistryLock)
            {
                _Registry.RemoveAll(w => !w.TryGetTarget(out ICacheTelemetrySource target) || ReferenceEquals(target, source));
            }
        }

        internal static string GetOperationSpanName(string operation)
        {
            if (operation != null && _OperationSpanNames.TryGetValue(operation, out string name)) return name;
            return CacheTelemetryNames.SpanOperationPrefix + operation;
        }

        internal static string GetPersistenceSpanName(string operation)
        {
            if (operation != null && _PersistenceSpanNames.TryGetValue(operation, out string name)) return name;
            return CacheTelemetryNames.SpanPersistencePrefix + operation;
        }

        internal static double ElapsedSeconds(long startTimestamp)
        {
            if (startTimestamp == 0) return 0;
            return (double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency;
        }

        #endregion

        #region Private-Methods

        private static Histogram<double> CreateDurationHistogram(string name, string description)
        {
            return Meter.CreateHistogram<double>(
                name,
                "s",
                description,
                null,
                new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });
        }

        private static List<ICacheTelemetrySource> Snapshot()
        {
            List<ICacheTelemetrySource> ret = new List<ICacheTelemetrySource>();

            lock (_RegistryLock)
            {
                _Registry.RemoveAll(w => !w.TryGetTarget(out _));

                foreach (WeakReference<ICacheTelemetrySource> weak in _Registry)
                {
                    if (weak.TryGetTarget(out ICacheTelemetrySource source) && source.TelemetryMetricsEnabled)
                        ret.Add(source);
                }
            }

            return ret;
        }

        private static IEnumerable<Measurement<long>> ObserveLong(Func<ICacheTelemetrySource, long> selector)
        {
            List<Measurement<long>> ret = new List<Measurement<long>>();

            try
            {
                foreach (ICacheTelemetrySource source in Snapshot())
                {
                    try
                    {
                        ret.Add(new Measurement<long>(selector(source), CacheTags(source)));
                    }
                    catch (Exception)
                    {
                        // Best-effort: a cache disposed mid-observation is skipped.
                    }
                }
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }

            return ret;
        }

        private static IEnumerable<Measurement<double>> ObserveDouble(Func<ICacheTelemetrySource, double> selector, bool skipZero)
        {
            List<Measurement<double>> ret = new List<Measurement<double>>();

            try
            {
                foreach (ICacheTelemetrySource source in Snapshot())
                {
                    try
                    {
                        double value = selector(source);
                        if (skipZero && value <= 0) continue;
                        ret.Add(new Measurement<double>(value, CacheTags(source)));
                    }
                    catch (Exception)
                    {
                        // Best-effort: a cache disposed mid-observation is skipped.
                    }
                }
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }

            return ret;
        }

        private static IEnumerable<Measurement<long>> ObserveBuildInfo()
        {
            List<Measurement<long>> ret = new List<Measurement<long>>();

            try
            {
                foreach (ICacheTelemetrySource source in Snapshot())
                {
                    ret.Add(new Measurement<long>(
                        1,
                        new KeyValuePair<string, object>(CacheTelemetryNames.AttributeCacheName, source.TelemetryCacheName),
                        new KeyValuePair<string, object>(CacheTelemetryNames.AttributeCacheType, source.TelemetryCacheType),
                        new KeyValuePair<string, object>(CacheTelemetryNames.AttributeVersion, Version)));
                }
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }

            return ret;
        }

        private static KeyValuePair<string, object>[] CacheTags(ICacheTelemetrySource source)
        {
            return new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>(CacheTelemetryNames.AttributeCacheName, source.TelemetryCacheName),
                new KeyValuePair<string, object>(CacheTelemetryNames.AttributeCacheType, source.TelemetryCacheType)
            };
        }

        private static string ResolveVersion()
        {
            try
            {
                Assembly assembly = typeof(CacheInstrumentation).Assembly;
                AssemblyInformationalVersionAttribute info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                string version = info?.InformationalVersion;

                if (String.IsNullOrEmpty(version))
                    version = assembly.GetName().Version?.ToString();

                if (String.IsNullOrEmpty(version)) return "unknown";

                int plus = version.IndexOf('+');
                return plus > 0 ? version.Substring(0, plus) : version;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        #endregion
    }
}
