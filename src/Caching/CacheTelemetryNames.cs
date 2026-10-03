namespace Caching
{
    /// <summary>
    /// Stable public names for every telemetry point emitted by the Caching library.
    /// Meter, activity source, instrument, attribute, and span names are public contract consumed by dashboards and alerts.
    /// Do not rename them without a major version change.
    /// </summary>
    public static class CacheTelemetryNames
    {
        #region Sources

        /// <summary>
        /// Name of the System.Diagnostics.Metrics.Meter used by the library.
        /// Subscribe a collector to this name to receive cache metrics.
        /// </summary>
        public const string MeterName = "Caching";

        /// <summary>
        /// Name of the System.Diagnostics.ActivitySource used by the library.
        /// Subscribe a collector to this name to receive cache spans.
        /// </summary>
        public const string ActivitySourceName = "Caching";

        #endregion

        #region Metrics

        /// <summary>
        /// Histogram (seconds). Duration of a public cache operation, labeled by cache, operation, and outcome.
        /// </summary>
        public const string OperationDuration = "caching.operation.duration";

        /// <summary>
        /// Counter ({lookup}). Cache lookups, labeled by hit or miss.
        /// </summary>
        public const string Lookups = "caching.lookups";

        /// <summary>
        /// Counter ({entry}). Entries evicted, labeled by eviction reason (capacity or memory).
        /// </summary>
        public const string Evictions = "caching.evictions";

        /// <summary>
        /// Counter ({entry}). Entries removed by the background expiration task.
        /// </summary>
        public const string Expirations = "caching.expirations";

        /// <summary>
        /// Counter ({entry}). Entries loaded from the persistence driver by Prepopulate.
        /// </summary>
        public const string Prepopulated = "caching.prepopulated";

        /// <summary>
        /// Counter ({error}). Failures, labeled by component and error type.
        /// </summary>
        public const string Errors = "caching.errors";

        /// <summary>
        /// Histogram (seconds). Duration of a call into the persistence driver, labeled by persistence operation and outcome.
        /// </summary>
        public const string PersistenceDuration = "caching.persistence.duration";

        /// <summary>
        /// Counter ({call}). Calls into the persistence driver, labeled by persistence operation and outcome.
        /// </summary>
        public const string PersistenceCalls = "caching.persistence.calls";

        /// <summary>
        /// Histogram (seconds). Duration of one background expiration sweep, labeled by outcome.
        /// </summary>
        public const string ExpirationSweepDuration = "caching.expiration.sweep.duration";

        /// <summary>
        /// Gauge (seconds). Unix time of the last expiration sweep that completed successfully.
        /// </summary>
        public const string ExpirationSweepLastSuccess = "caching.expiration.sweep.last_success";

        /// <summary>
        /// Histogram (seconds). Time spent waiting for the single-slot atomic lock used by GetOrAdd and AddOrUpdate.
        /// </summary>
        public const string LockWaitDuration = "caching.lock.wait.duration";

        /// <summary>
        /// Gauge ({operation}). Operations currently waiting for the atomic lock.
        /// </summary>
        public const string LockWaiting = "caching.lock.waiting";

        /// <summary>
        /// Gauge ({entry}). Current number of entries.
        /// </summary>
        public const string Entries = "caching.entries";

        /// <summary>
        /// Gauge ({entry}). Configured maximum number of entries.
        /// </summary>
        public const string Capacity = "caching.capacity";

        /// <summary>
        /// Gauge ({entry}). Configured number of entries evicted when capacity is reached.
        /// </summary>
        public const string EvictCount = "caching.evict_count";

        /// <summary>
        /// Gauge (bytes). Current estimated memory usage.
        /// </summary>
        public const string MemoryUsage = "caching.memory.usage";

        /// <summary>
        /// Gauge (bytes). Configured memory limit. Zero means no limit.
        /// </summary>
        public const string MemoryLimit = "caching.memory.limit";

        /// <summary>
        /// Gauge (seconds). Configured interval between expiration sweeps.
        /// </summary>
        public const string ExpirationInterval = "caching.expiration.interval";

        /// <summary>
        /// Gauge ({cache}). Always 1 per live cache instance, labeled with the library version. Used as build info.
        /// </summary>
        public const string BuildInfo = "caching.build.info";

        #endregion

        #region Attributes

        /// <summary>
        /// Attribute: cache name, from CacheBase.Name. Must be a small, static set of values.
        /// </summary>
        public const string AttributeCacheName = "cache.name";

        /// <summary>
        /// Attribute: cache eviction policy, one of fifo or lru.
        /// </summary>
        public const string AttributeCacheType = "cache.type";

        /// <summary>
        /// Attribute: public cache operation, for example get, add_replace, get_or_add.
        /// </summary>
        public const string AttributeOperation = "cache.operation";

        /// <summary>
        /// Attribute: outcome of an operation, one of success, miss, or error.
        /// </summary>
        public const string AttributeOutcome = "outcome";

        /// <summary>
        /// Attribute: lookup result, one of hit or miss.
        /// </summary>
        public const string AttributeLookupResult = "cache.lookup.result";

        /// <summary>
        /// Attribute: eviction reason, one of capacity or memory.
        /// </summary>
        public const string AttributeEvictionReason = "cache.eviction.reason";

        /// <summary>
        /// Attribute: failing component, one of operation, persistence, or expiration.
        /// </summary>
        public const string AttributeComponent = "cache.component";

        /// <summary>
        /// Attribute: persistence driver operation, one of write, delete, clear, get, or enumerate.
        /// </summary>
        public const string AttributePersistenceOperation = "cache.persistence.operation";

        /// <summary>
        /// Span-only attribute: persistence driver type name.
        /// </summary>
        public const string AttributePersistenceDriver = "cache.persistence.driver";

        /// <summary>
        /// Attribute: exception type name (OpenTelemetry semantic convention).
        /// </summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>
        /// Attribute: library version, on the build info gauge.
        /// </summary>
        public const string AttributeVersion = "cache.version";

        /// <summary>
        /// Span-only attribute: number of entries affected (evicted, expired, loaded).
        /// </summary>
        public const string AttributeEntryCount = "cache.entry_count";

        /// <summary>
        /// Span-only attribute: lookup hit (true) or miss (false).
        /// </summary>
        public const string AttributeHit = "cache.hit";

        #endregion

        #region Spans

        /// <summary>
        /// Span name prefix for public operations. Spans are named "caching {operation}".
        /// </summary>
        public const string SpanOperationPrefix = "caching ";

        /// <summary>
        /// Span name prefix for persistence driver client spans. Spans are named "persistence {operation}".
        /// </summary>
        public const string SpanPersistencePrefix = "persistence ";

        /// <summary>
        /// Span name for a background expiration sweep that removed at least one entry or failed.
        /// </summary>
        public const string SpanExpirationSweep = "caching expiration_sweep";

        /// <summary>
        /// Span name for the user-supplied value factory stage inside GetOrAdd and AddOrUpdate.
        /// </summary>
        public const string SpanValueFactory = "stage:value_factory";

        /// <summary>
        /// Span name for the atomic lock wait stage inside GetOrAdd and AddOrUpdate.
        /// </summary>
        public const string SpanLockWait = "stage:lock_wait";

        #endregion

        #region Values

        /// <summary>
        /// Operation value: Get.
        /// </summary>
        public const string OperationGet = "get";

        /// <summary>
        /// Operation value: GetOrDefault.
        /// </summary>
        public const string OperationGetOrDefault = "get_or_default";

        /// <summary>
        /// Operation value: TryGet.
        /// </summary>
        public const string OperationTryGet = "try_get";

        /// <summary>
        /// Operation value: Contains.
        /// </summary>
        public const string OperationContains = "contains";

        /// <summary>
        /// Operation value: AddReplace and AddReplaceAsync.
        /// </summary>
        public const string OperationAddReplace = "add_replace";

        /// <summary>
        /// Operation value: GetOrAdd and GetOrAddAsync.
        /// </summary>
        public const string OperationGetOrAdd = "get_or_add";

        /// <summary>
        /// Operation value: AddOrUpdate and AddOrUpdateAsync.
        /// </summary>
        public const string OperationAddOrUpdate = "add_or_update";

        /// <summary>
        /// Operation value: Remove and RemoveAsync.
        /// </summary>
        public const string OperationRemove = "remove";

        /// <summary>
        /// Operation value: TryRemove.
        /// </summary>
        public const string OperationTryRemove = "try_remove";

        /// <summary>
        /// Operation value: Clear and ClearAsync.
        /// </summary>
        public const string OperationClear = "clear";

        /// <summary>
        /// Operation value: Prepopulate and PrepopulateAsync.
        /// </summary>
        public const string OperationPrepopulate = "prepopulate";

        /// <summary>
        /// Outcome value: completed successfully.
        /// </summary>
        public const string OutcomeSuccess = "success";

        /// <summary>
        /// Outcome value: lookup completed but the key was not present.
        /// </summary>
        public const string OutcomeMiss = "miss";

        /// <summary>
        /// Outcome value: failed with an exception.
        /// </summary>
        public const string OutcomeError = "error";

        /// <summary>
        /// Outcome value: canceled through a CancellationToken.
        /// </summary>
        public const string OutcomeCanceled = "canceled";

        /// <summary>
        /// Lookup result value: hit.
        /// </summary>
        public const string LookupHit = "hit";

        /// <summary>
        /// Lookup result value: miss.
        /// </summary>
        public const string LookupMiss = "miss";

        /// <summary>
        /// Eviction reason value: entry count reached capacity.
        /// </summary>
        public const string EvictionReasonCapacity = "capacity";

        /// <summary>
        /// Eviction reason value: estimated memory reached MaxMemoryBytes.
        /// </summary>
        public const string EvictionReasonMemory = "memory";

        /// <summary>
        /// Component value: public cache operation.
        /// </summary>
        public const string ComponentOperation = "operation";

        /// <summary>
        /// Component value: persistence driver call.
        /// </summary>
        public const string ComponentPersistence = "persistence";

        /// <summary>
        /// Component value: background expiration task.
        /// </summary>
        public const string ComponentExpiration = "expiration";

        /// <summary>
        /// Persistence operation value: WriteAsync.
        /// </summary>
        public const string PersistenceWrite = "write";

        /// <summary>
        /// Persistence operation value: DeleteAsync.
        /// </summary>
        public const string PersistenceDelete = "delete";

        /// <summary>
        /// Persistence operation value: ClearAsync.
        /// </summary>
        public const string PersistenceClear = "clear";

        /// <summary>
        /// Persistence operation value: GetAsync.
        /// </summary>
        public const string PersistenceGet = "get";

        /// <summary>
        /// Persistence operation value: EnumerateAsync.
        /// </summary>
        public const string PersistenceEnumerate = "enumerate";

        /// <summary>
        /// Cache type value: FIFOCache.
        /// </summary>
        public const string CacheTypeFifo = "fifo";

        /// <summary>
        /// Cache type value: LRUCache.
        /// </summary>
        public const string CacheTypeLru = "lru";

        /// <summary>
        /// Default cache name used when CacheBase.Name is not set.
        /// </summary>
        public const string DefaultCacheName = "default";

        #endregion
    }
}
