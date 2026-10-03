namespace Caching
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cache base class.
    /// </summary>
    public abstract class CacheBase<T1, T2> : IDisposable, ICacheTelemetrySource
    {
        #region Public-Members

        /// <summary>
        /// Cache name, emitted as the cache.name label on every metric and span so several caches in one process can be told apart.
        /// Use a short, static value (for example "sessions" or "products"). Never use per-request or per-user values: the name is a metric label.
        /// Default is "default". Null or empty resets to the default.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = String.IsNullOrEmpty(value) ? CacheTelemetryNames.DefaultCacheName : value;
            }
        }

        /// <summary>
        /// Telemetry settings for this cache instance. Metrics and spans are emitted through the Meter and ActivitySource
        /// named in CacheTelemetryNames and cost effectively nothing until a collector subscribes.
        /// Setting null restores the defaults.
        /// </summary>
        public CacheTelemetrySettings Telemetry
        {
            get
            {
                return _Telemetry;
            }
            set
            {
                _Telemetry = value ?? new CacheTelemetrySettings();
            }
        }

        /// <summary>
        /// Cancellation token.
        /// </summary>
        public CancellationToken Token
        {
            get
            {
                return _Token;
            }
            set
            {
                _Token = value;
            }
        }

        /// <summary>
        /// Cache events.
        /// </summary>
        public CacheEvents<T1, T2> Events
        {
            get
            {
                return _Events;
            }
            set
            {
                if (value == null) _Events = new CacheEvents<T1, T2>();
                else _Events = value;
            }
        }

        /// <summary>
        /// Persistence driver.
        /// </summary>
        public IPersistenceDriver<T1, T2> Persistence
        {
            get
            {
                return _Persistence;
            }
            set
            {
                _Persistence = value;
            }
        }

        /// <summary>
        /// Cache capacity.
        /// </summary>
        public int Capacity { get; internal set; } = 0;

        /// <summary>
        /// Number of entries to evict when cache reaches capacity.
        /// </summary>
        public int EvictCount { get; internal set; } = 0;

        /// <summary>
        /// Frequency with which the cache is evaluated for expired entries. Default is 1000ms, minimum is 1ms.
        /// A change takes effect immediately: the pending wait is shortened or lengthened to the new interval.
        /// </summary>
        public int ExpirationIntervalMs
        {
            get
            {
                return _ExpirationIntervalMs;
            }
            set
            {
                if (value < 1) throw new ArgumentException("ExpirationIntervalMs must be at least 1ms.");
                _ExpirationIntervalMs = value;
                WakeExpirationTask();
            }
        }

        /// <summary>
        /// If true, accessing an item refreshes its expiration time.
        /// </summary>
        public bool SlidingExpiration { get; set; } = false;

        /// <summary>
        /// Maximum memory limit in bytes (0 = no limit).
        /// </summary>
        public long MaxMemoryBytes { get; set; } = 0;

        /// <summary>
        /// Current estimated memory usage in bytes.
        /// </summary>
        public long CurrentMemoryBytes { get; protected set; } = 0;

        /// <summary>
        /// Function to estimate size of values.
        /// </summary>
        public Func<T2, long> SizeEstimator { get; set; }

        /// <summary>
        /// Total number of cache hits.
        /// </summary>
        public long HitCount => _hitCount;

        /// <summary>
        /// Total number of cache misses.
        /// </summary>
        public long MissCount => _missCount;

        /// <summary>
        /// Total number of evicted entries.
        /// </summary>
        public long EvictionCount => _evictionCount;

        /// <summary>
        /// Total number of expired entries.
        /// </summary>
        public long ExpirationCount => _expirationCount;

        /// <summary>
        /// Cache hit rate (0.0 to 1.0).
        /// </summary>
        public double HitRate
        {
            get
            {
                long total = _hitCount + _missCount;
                return total == 0 ? 0.0 : (double)_hitCount / total;
            }
        }

        /// <summary>
        /// Equality comparer used for keys.
        /// </summary>
        public IEqualityComparer<T1> KeyComparer => _KeyComparer;

        #endregion

        #region Internal-Members

        internal CancellationTokenSource _TokenSource = new CancellationTokenSource();
        internal CancellationToken _Token;

        internal int _ExpirationIntervalMs = 1000;
        internal readonly object _CacheLock = new object();
        internal Dictionary<T1, DataNode<T2>> _Cache;
        internal IPersistenceDriver<T1, T2> _Persistence = null;
        internal CacheEvents<T1, T2> _Events = new CacheEvents<T1, T2>();
        internal Task _ExpirationTaskInstance = null;
        internal bool _disposed = false;
        internal IEqualityComparer<T1> _KeyComparer;
        internal readonly SemaphoreSlim _AtomicLock = new SemaphoreSlim(1, 1);
        internal readonly SemaphoreSlim _ExpirationWake = new SemaphoreSlim(0, 1);

        internal long _hitCount = 0;
        internal long _missCount = 0;
        internal long _evictionCount = 0;
        internal long _expirationCount = 0;

        internal string _Name = CacheTelemetryNames.DefaultCacheName;
        internal CacheTelemetrySettings _Telemetry = new CacheTelemetrySettings();
        internal ActivityContext _CreationContext = default;
        internal long _LockWaiting = 0;
        internal long _LastSweepSuccessTicks = 0;

        private static readonly long _UnixEpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        internal abstract Task ExpirationTask(CancellationToken token = default);

        internal abstract string TelemetryCacheType { get; }

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of the object. Do not use after disposal.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Retrieve the current number of entries in the cache.
        /// </summary>
        /// <returns>An integer containing the number of entries.</returns>
        public abstract int Count();

        /// <summary>
        /// Retrieve the key of the oldest entry in the cache.
        /// </summary>
        /// <returns>Key of the oldest entry.</returns>
        public abstract T1 Oldest();

        /// <summary>
        /// Retrieve the key of the newest entry in the cache.
        /// </summary>
        /// <returns>Key of the newest entry.</returns>
        public abstract T1 Newest();

        /// <summary>
        /// Retrieve all entries from the cache.
        /// </summary>
        /// <returns>Dictionary of all entries.</returns>
        public abstract Dictionary<T1, T2> All();

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// Clear the cache asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <returns>The object data associated with the key.</returns>
        public abstract T2 Get(T1 key);

        /// <summary>
        /// Retrieve a key's value from the cache, or return a default value if not found.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <param name="defaultValue">Value to return if key is not found.</param>
        /// <returns>The object data associated with the key, or defaultValue if not found.</returns>
        public abstract T2 GetOrDefault(T1 key, T2 defaultValue = default);

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <returns>True if key is found.</returns>
        public abstract bool TryGet(T1 key, out T2 val);

        /// <summary>
        /// See if a key exists in the cache.
        /// </summary>
        /// <param name="key">The key of the cached items.</param>
        /// <returns>True if cached.</returns>
        public abstract bool Contains(T1 key);

        /// <summary>
        /// Add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        public abstract void AddReplace(T1 key, T2 val, DateTime? expiration = null);

        /// <summary>
        /// Add or replace a key's value in the cache with relative expiration time.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiresIn">Time span until expiration.</param>
        public void AddReplace(T1 key, T2 val, TimeSpan expiresIn)
        {
            AddReplace(key, val, DateTime.UtcNow.Add(expiresIn));
        }

        /// <summary>
        /// Add or replace a key's value in the cache asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task AddReplaceAsync(T1 key, T2 val, DateTime? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add or replace a key's value in the cache asynchronously with relative expiration time.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiresIn">Time span until expiration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task AddReplaceAsync(T1 key, T2 val, TimeSpan expiresIn, CancellationToken cancellationToken = default)
        {
            return AddReplaceAsync(key, val, DateTime.UtcNow.Add(expiresIn), cancellationToken);
        }

        /// <summary>
        /// Attempt to add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>True if successful.</returns>
        public abstract bool TryAddReplace(T1 key, T2 val, DateTime? expiration = null);

        /// <summary>
        /// Get a value from cache, or add it if not present.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Function to create value if not present.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>The value.</returns>
        public abstract T2 GetOrAdd(T1 key, Func<T1, T2> valueFactory, DateTime? expiration = null);

        /// <summary>
        /// Get a value from cache, or add it if not present (with relative expiration).
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Function to create value if not present.</param>
        /// <param name="expiresIn">Time span until expiration.</param>
        /// <returns>The value.</returns>
        public T2 GetOrAdd(T1 key, Func<T1, T2> valueFactory, TimeSpan? expiresIn)
        {
            DateTime? expiration = expiresIn.HasValue
                ? DateTime.UtcNow.Add(expiresIn.Value)
                : (DateTime?)null;
            return GetOrAdd(key, valueFactory, expiration);
        }

        /// <summary>
        /// Get a value from cache, or add it if not present asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Async function to create value if not present.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The value.</returns>
        public abstract Task<T2> GetOrAddAsync(T1 key, Func<T1, Task<T2>> valueFactory, DateTime? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a value from cache, or add it if not present asynchronously (with relative expiration).
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Async function to create value if not present.</param>
        /// <param name="expiresIn">Time span until expiration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The value.</returns>
        public Task<T2> GetOrAddAsync(T1 key, Func<T1, Task<T2>> valueFactory, TimeSpan? expiresIn, CancellationToken cancellationToken = default)
        {
            DateTime? expiration = expiresIn.HasValue
                ? DateTime.UtcNow.Add(expiresIn.Value)
                : (DateTime?)null;
            return GetOrAddAsync(key, valueFactory, expiration, cancellationToken);
        }

        /// <summary>
        /// Try to get a value from cache, or add it if not present.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Function to create value if not present.</param>
        /// <param name="value">The retrieved or created value.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>True if successful.</returns>
        public abstract bool TryGetOrAdd(T1 key, Func<T1, T2> valueFactory, out T2 value, DateTime? expiration = null);

        /// <summary>
        /// Add a new value or update existing value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="addValue">Value to add if key doesn't exist.</param>
        /// <param name="updateValueFactory">Function to update value if key exists.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>The resulting value.</returns>
        public abstract T2 AddOrUpdate(T1 key, T2 addValue, Func<T1, T2, T2> updateValueFactory, DateTime? expiration = null);

        /// <summary>
        /// Add a new value or update existing value asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="addValue">Value to add if key doesn't exist.</param>
        /// <param name="updateValueFactory">Async function to update value if key exists.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resulting value.</returns>
        public abstract Task<T2> AddOrUpdateAsync(T1 key, T2 addValue, Func<T1, T2, Task<T2>> updateValueFactory, DateTime? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove a key from the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        public abstract void Remove(T1 key);

        /// <summary>
        /// Remove a key from the cache asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task RemoveAsync(T1 key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempt to remove a key and its value from the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The removed value, if found.</param>
        /// <returns>True if the key was found and removed.</returns>
        public abstract bool TryRemove(T1 key, out T2 val);

        /// <summary>
        /// Retrieve all keys in the cache.
        /// </summary>
        /// <returns>List of keys.</returns>
        public abstract List<T1> GetKeys();

        /// <summary>
        /// Prepopulate the cache with entries from the persistence layer.
        /// </summary>
        public abstract void Prepopulate();

        /// <summary>
        /// Prepopulate the cache with entries from the persistence layer asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task PrepopulateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get current cache statistics.
        /// </summary>
        /// <returns>Cache statistics.</returns>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                HitCount = _hitCount,
                MissCount = _missCount,
                EvictionCount = _evictionCount,
                ExpirationCount = _expirationCount,
                HitRate = HitRate,
                CurrentCount = Count(),
                Capacity = Capacity,
                CurrentMemoryBytes = CurrentMemoryBytes
            };
        }

        /// <summary>
        /// Reset all statistics counters.
        /// </summary>
        public void ResetStatistics()
        {
            Interlocked.Exchange(ref _hitCount, 0);
            Interlocked.Exchange(ref _missCount, 0);
            Interlocked.Exchange(ref _evictionCount, 0);
            Interlocked.Exchange(ref _expirationCount, 0);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Throw if disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Estimate size of a value.
        /// </summary>
        /// <param name="value">Value to estimate.</param>
        /// <returns>Estimated size in bytes.</returns>
        protected long EstimateSize(T2 value)
        {
            if (SizeEstimator != null)
                return SizeEstimator(value);

            // Default estimations
            if (value is string str)
                return str.Length * 2; // Unicode chars

            if (value is byte[] bytes)
                return bytes.Length;

            return 0; // Unknown
        }

        /// <summary>
        /// Mark a cache entry as used and refresh sliding expiration when enabled.
        /// </summary>
        /// <param name="node">Node that was accessed.</param>
        protected void MarkAccessed(DataNode<T2> node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            DateTime now = DateTime.UtcNow;
            node.LastUsed = now;

            if (SlidingExpiration && node.Expiration.HasValue && node.ExpirationDuration.HasValue)
            {
                node.Expiration = now.Add(node.ExpirationDuration.Value);
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Wait until the next expiration sweep is due. Re-evaluates the remaining time whenever ExpirationIntervalMs changes,
        /// measured from the start of the wait, so a shorter interval applies to the pending wait instead of the next one.
        /// </summary>
        internal async Task WaitForNextSweepAsync(CancellationToken token)
        {
            long waitStart = Stopwatch.GetTimestamp();

            while (true)
            {
                long elapsedMs = (Stopwatch.GetTimestamp() - waitStart) * 1000 / Stopwatch.Frequency;
                long remainingMs = _ExpirationIntervalMs - elapsedMs;
                if (remainingMs <= 0) return;

                bool woken = await _ExpirationWake.WaitAsync((int)Math.Min(remainingMs, Int32.MaxValue), token).ConfigureAwait(false);
                if (!woken) return;
            }
        }

        internal void WakeExpirationTask()
        {
            try
            {
                if (_ExpirationWake.CurrentCount == 0) _ExpirationWake.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already signaled.
            }
        }

        bool ICacheTelemetrySource.TelemetryMetricsEnabled => !_disposed && MetricsEnabled;

        string ICacheTelemetrySource.TelemetryCacheName => _Name;

        string ICacheTelemetrySource.TelemetryCacheType => TelemetryCacheType;

        long ICacheTelemetrySource.TelemetryEntryCount
        {
            get
            {
                Dictionary<T1, DataNode<T2>> cache = _Cache;
                return cache != null ? cache.Count : 0;
            }
        }

        long ICacheTelemetrySource.TelemetryCapacity => Capacity;

        long ICacheTelemetrySource.TelemetryEvictCount => EvictCount;

        long ICacheTelemetrySource.TelemetryMemoryUsage => CurrentMemoryBytes;

        long ICacheTelemetrySource.TelemetryMemoryLimit => MaxMemoryBytes;

        double ICacheTelemetrySource.TelemetryExpirationIntervalSeconds => _ExpirationIntervalMs / 1000.0;

        long ICacheTelemetrySource.TelemetryLockWaiting => Interlocked.Read(ref _LockWaiting);

        double ICacheTelemetrySource.TelemetryLastSweepSuccessUnixSeconds
        {
            get
            {
                long ticks = Interlocked.Read(ref _LastSweepSuccessTicks);
                if (ticks == 0) return 0;
                return (ticks - _UnixEpochTicks) / (double)TimeSpan.TicksPerSecond;
            }
        }

        internal bool MetricsEnabled
        {
            get
            {
                CacheTelemetrySettings settings = _Telemetry;
                return settings.Enable && settings.EnableMetrics;
            }
        }

        internal bool TracesEnabled
        {
            get
            {
                CacheTelemetrySettings settings = _Telemetry;
                return settings.Enable && settings.EnableTraces && CacheInstrumentation.Source.HasListeners();
            }
        }

        /// <summary>
        /// Register with the process-wide gauges and capture the creation trace context. Called at the end of each constructor.
        /// </summary>
        internal void RegisterTelemetry()
        {
            try
            {
                Activity current = Activity.Current;
                if (current != null) _CreationContext = current.Context;
                CacheInstrumentation.Register(this);
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        internal void UnregisterTelemetry()
        {
            try
            {
                CacheInstrumentation.Unregister(this);
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        internal long OperationTimestamp()
        {
            return (MetricsEnabled && CacheInstrumentation.OperationDuration.Enabled) ? Stopwatch.GetTimestamp() : 0;
        }

        internal Activity StartOperationActivity(string operation, bool isLookup = false)
        {
            try
            {
                if (!TracesEnabled) return null;
                if (isLookup && !_Telemetry.TraceLookups) return null;

                Activity activity = CacheInstrumentation.Source.StartActivity(
                    CacheInstrumentation.GetOperationSpanName(operation),
                    ActivityKind.Internal);

                if (activity != null && activity.IsAllDataRequested)
                {
                    activity.SetTag(CacheTelemetryNames.AttributeCacheName, _Name);
                    activity.SetTag(CacheTelemetryNames.AttributeCacheType, TelemetryCacheType);
                    activity.SetTag(CacheTelemetryNames.AttributeOperation, operation);
                }

                return activity;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal void CompleteOperation(string operation, long startTimestamp, string outcome, Activity activity)
        {
            try
            {
                if (startTimestamp != 0)
                {
                    TagList tags = CacheTags();
                    tags.Add(CacheTelemetryNames.AttributeOperation, operation);
                    tags.Add(CacheTelemetryNames.AttributeOutcome, outcome);
                    CacheInstrumentation.OperationDuration.Record(CacheInstrumentation.ElapsedSeconds(startTimestamp), tags);
                }

                if (activity != null)
                {
                    activity.SetTag(CacheTelemetryNames.AttributeOutcome, outcome);
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        /// <summary>
        /// Record a failed operation. Always returns false so it can be used as an exception filter without catching.
        /// A KeyNotFoundException from Get is recorded as a miss, not an error.
        /// </summary>
        internal bool FailOperation(string operation, long startTimestamp, Exception exception, Activity activity)
        {
            if (operation == CacheTelemetryNames.OperationGet && exception is KeyNotFoundException)
            {
                CompleteOperation(operation, startTimestamp, CacheTelemetryNames.OutcomeMiss, activity);
                return false;
            }

            try
            {
                string outcome = Outcome(exception);
                string errorType = exception.GetType().Name;

                if (startTimestamp != 0)
                {
                    TagList tags = CacheTags();
                    tags.Add(CacheTelemetryNames.AttributeOperation, operation);
                    tags.Add(CacheTelemetryNames.AttributeOutcome, outcome);
                    tags.Add(CacheTelemetryNames.AttributeErrorType, errorType);
                    CacheInstrumentation.OperationDuration.Record(CacheInstrumentation.ElapsedSeconds(startTimestamp), tags);
                }

                if (outcome == CacheTelemetryNames.OutcomeError)
                    RecordError(CacheTelemetryNames.ComponentOperation, errorType);

                RecordActivityException(activity, exception, outcome);
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }

            return false;
        }

        internal void RecordHit()
        {
            Interlocked.Increment(ref _hitCount);
            RecordLookup(CacheTelemetryNames.LookupHit, true);
        }

        internal void RecordMiss()
        {
            Interlocked.Increment(ref _missCount);
            RecordLookup(CacheTelemetryNames.LookupMiss, false);
        }

        internal void RecordEvictions(int count, string reason)
        {
            Interlocked.Add(ref _evictionCount, count);

            try
            {
                if (count > 0 && MetricsEnabled && CacheInstrumentation.Evictions.Enabled)
                {
                    TagList tags = CacheTags();
                    tags.Add(CacheTelemetryNames.AttributeEvictionReason, reason);
                    CacheInstrumentation.Evictions.Add(count, tags);
                }
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        internal void RecordExpirations(int count)
        {
            Interlocked.Add(ref _expirationCount, count);

            try
            {
                if (count > 0 && MetricsEnabled && CacheInstrumentation.Expirations.Enabled)
                    CacheInstrumentation.Expirations.Add(count, CacheTags());
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        internal void RecordPrepopulated(int count, Activity activity)
        {
            try
            {
                if (activity != null) activity.SetTag(CacheTelemetryNames.AttributeEntryCount, count);

                if (count > 0 && MetricsEnabled && CacheInstrumentation.Prepopulated.Enabled)
                    CacheInstrumentation.Prepopulated.Add(count, CacheTags());
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        internal async Task InvokePersistenceAsync(string operation, Func<IPersistenceDriver<T1, T2>, Task> call)
        {
            IPersistenceDriver<T1, T2> driver = _Persistence;
            if (driver == null) return;

            long start = Stopwatch.GetTimestamp();
            Activity activity = StartPersistenceActivity(operation, driver);

            try
            {
                await call(driver).ConfigureAwait(false);
                CompletePersistence(operation, start, activity);
            }
            catch (Exception e) when (FailPersistence(operation, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        internal async Task<TResult> InvokePersistenceAsync<TResult>(string operation, Func<IPersistenceDriver<T1, T2>, Task<TResult>> call)
        {
            IPersistenceDriver<T1, T2> driver = _Persistence;
            if (driver == null) return default;

            long start = Stopwatch.GetTimestamp();
            Activity activity = StartPersistenceActivity(operation, driver);

            try
            {
                TResult ret = await call(driver).ConfigureAwait(false);
                CompletePersistence(operation, start, activity);
                return ret;
            }
            catch (Exception e) when (FailPersistence(operation, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        internal T2 InvokeValueFactory(Func<T2> factory)
        {
            Activity activity = StartStageActivity(CacheTelemetryNames.SpanValueFactory);

            try
            {
                T2 ret = factory();
                activity?.SetStatus(ActivityStatusCode.Ok);
                return ret;
            }
            catch (Exception e) when (RecordActivityException(activity, e, Outcome(e)))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        internal async Task<T2> InvokeValueFactoryAsync(Func<Task<T2>> factory)
        {
            Activity activity = StartStageActivity(CacheTelemetryNames.SpanValueFactory);

            try
            {
                T2 ret = await factory().ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return ret;
            }
            catch (Exception e) when (RecordActivityException(activity, e, Outcome(e)))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        internal void WaitAtomicLock(string operation)
        {
            long start = Stopwatch.GetTimestamp();
            Activity activity = StartStageActivity(CacheTelemetryNames.SpanLockWait);
            Interlocked.Increment(ref _LockWaiting);

            try
            {
                _AtomicLock.Wait();
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception e) when (RecordActivityException(activity, e, Outcome(e)))
            {
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _LockWaiting);
                RecordLockWait(operation, start);
                activity?.Dispose();
            }
        }

        internal async Task WaitAtomicLockAsync(string operation, CancellationToken cancellationToken)
        {
            long start = Stopwatch.GetTimestamp();
            Activity activity = StartStageActivity(CacheTelemetryNames.SpanLockWait);
            Interlocked.Increment(ref _LockWaiting);

            try
            {
                await _AtomicLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception e) when (RecordActivityException(activity, e, Outcome(e)))
            {
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _LockWaiting);
                RecordLockWait(operation, start);
                activity?.Dispose();
            }
        }

        /// <summary>
        /// Start the span for an expiration sweep. Sweeps that remove nothing produce no span, so the span is started
        /// retroactively at the sweep start time once there is work. The span is a trace root linked to the trace that created the cache.
        /// </summary>
        internal Activity StartSweepActivity(DateTime startUtc, int expiredCount)
        {
            try
            {
                if (!TracesEnabled) return null;

                List<ActivityLink> links = null;
                if (_CreationContext != default(ActivityContext))
                    links = new List<ActivityLink> { new ActivityLink(_CreationContext) };

                Activity activity = CacheInstrumentation.Source.StartActivity(
                    CacheTelemetryNames.SpanExpirationSweep,
                    ActivityKind.Internal,
                    default(ActivityContext),
                    null,
                    links,
                    new DateTimeOffset(startUtc));

                if (activity != null && activity.IsAllDataRequested)
                {
                    activity.SetTag(CacheTelemetryNames.AttributeCacheName, _Name);
                    activity.SetTag(CacheTelemetryNames.AttributeCacheType, TelemetryCacheType);
                    activity.SetTag(CacheTelemetryNames.AttributeEntryCount, expiredCount);
                }

                return activity;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal void CompleteSweep(long startTimestamp, Activity activity)
        {
            Interlocked.Exchange(ref _LastSweepSuccessTicks, DateTime.UtcNow.Ticks);

            try
            {
                if (MetricsEnabled && CacheInstrumentation.ExpirationSweepDuration.Enabled)
                {
                    TagList tags = CacheTags();
                    tags.Add(CacheTelemetryNames.AttributeOutcome, CacheTelemetryNames.OutcomeSuccess);
                    CacheInstrumentation.ExpirationSweepDuration.Record(CacheInstrumentation.ElapsedSeconds(startTimestamp), tags);
                }

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        /// <summary>
        /// Record a failed expiration sweep. Always returns false so it can be used as an exception filter.
        /// </summary>
        internal bool FailSweep(long startTimestamp, Exception exception, Activity activity)
        {
            try
            {
                string outcome = Outcome(exception);
                string errorType = exception.GetType().Name;

                if (MetricsEnabled && CacheInstrumentation.ExpirationSweepDuration.Enabled)
                {
                    TagList tags = CacheTags();
                    tags.Add(CacheTelemetryNames.AttributeOutcome, outcome);
                    if (outcome == CacheTelemetryNames.OutcomeError) tags.Add(CacheTelemetryNames.AttributeErrorType, errorType);
                    CacheInstrumentation.ExpirationSweepDuration.Record(CacheInstrumentation.ElapsedSeconds(startTimestamp), tags);
                }

                if (outcome == CacheTelemetryNames.OutcomeError)
                {
                    RecordError(CacheTelemetryNames.ComponentExpiration, errorType);

                    // A sweep that fails before any entry is found has no span yet. Create one so the failure is visible in traces.
                    Activity owned = activity == null ? StartSweepActivity(DateTime.UtcNow, 0) : null;
                    RecordActivityException(activity ?? owned, exception, outcome);
                    owned?.Dispose();
                }
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private TagList CacheTags()
        {
            TagList tags = new TagList();
            tags.Add(CacheTelemetryNames.AttributeCacheName, _Name);
            tags.Add(CacheTelemetryNames.AttributeCacheType, TelemetryCacheType);
            return tags;
        }

        private static string Outcome(Exception exception)
        {
            return exception is OperationCanceledException
                ? CacheTelemetryNames.OutcomeCanceled
                : CacheTelemetryNames.OutcomeError;
        }

        private void RecordLookup(string result, bool hit)
        {
            try
            {
                if (MetricsEnabled && CacheInstrumentation.Lookups.Enabled)
                {
                    TagList tags = CacheTags();
                    tags.Add(CacheTelemetryNames.AttributeLookupResult, result);
                    CacheInstrumentation.Lookups.Add(1, tags);
                }

                Activity current = Activity.Current;
                if (current != null && ReferenceEquals(current.Source, CacheInstrumentation.Source) && current.IsAllDataRequested)
                    current.SetTag(CacheTelemetryNames.AttributeHit, hit);
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        private void RecordError(string component, string errorType)
        {
            try
            {
                if (MetricsEnabled && CacheInstrumentation.Errors.Enabled)
                {
                    TagList tags = CacheTags();
                    tags.Add(CacheTelemetryNames.AttributeComponent, component);
                    tags.Add(CacheTelemetryNames.AttributeErrorType, errorType);
                    CacheInstrumentation.Errors.Add(1, tags);
                }
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        private void RecordLockWait(string operation, long startTimestamp)
        {
            try
            {
                if (MetricsEnabled && CacheInstrumentation.LockWaitDuration.Enabled)
                {
                    TagList tags = CacheTags();
                    tags.Add(CacheTelemetryNames.AttributeOperation, operation);
                    CacheInstrumentation.LockWaitDuration.Record(CacheInstrumentation.ElapsedSeconds(startTimestamp), tags);
                }
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        private Activity StartStageActivity(string name)
        {
            try
            {
                if (!TracesEnabled) return null;

                Activity activity = CacheInstrumentation.Source.StartActivity(name, ActivityKind.Internal);

                if (activity != null && activity.IsAllDataRequested)
                {
                    activity.SetTag(CacheTelemetryNames.AttributeCacheName, _Name);
                    activity.SetTag(CacheTelemetryNames.AttributeCacheType, TelemetryCacheType);
                }

                return activity;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Activity StartPersistenceActivity(string operation, IPersistenceDriver<T1, T2> driver)
        {
            try
            {
                if (!TracesEnabled) return null;

                Activity activity = CacheInstrumentation.Source.StartActivity(
                    CacheInstrumentation.GetPersistenceSpanName(operation),
                    ActivityKind.Client);

                if (activity != null && activity.IsAllDataRequested)
                {
                    activity.SetTag(CacheTelemetryNames.AttributeCacheName, _Name);
                    activity.SetTag(CacheTelemetryNames.AttributeCacheType, TelemetryCacheType);
                    activity.SetTag(CacheTelemetryNames.AttributePersistenceOperation, operation);
                    activity.SetTag(CacheTelemetryNames.AttributePersistenceDriver, driver.GetType().Name);
                }

                return activity;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void CompletePersistence(string operation, long startTimestamp, Activity activity)
        {
            try
            {
                RecordPersistenceMetrics(operation, startTimestamp, CacheTelemetryNames.OutcomeSuccess, null);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }
        }

        private bool FailPersistence(string operation, long startTimestamp, Exception exception, Activity activity)
        {
            try
            {
                string outcome = Outcome(exception);
                string errorType = exception.GetType().Name;

                RecordPersistenceMetrics(operation, startTimestamp, outcome, outcome == CacheTelemetryNames.OutcomeError ? errorType : null);
                if (outcome == CacheTelemetryNames.OutcomeError) RecordError(CacheTelemetryNames.ComponentPersistence, errorType);
                RecordActivityException(activity, exception, outcome);
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }

            return false;
        }

        private void RecordPersistenceMetrics(string operation, long startTimestamp, string outcome, string errorType)
        {
            if (!MetricsEnabled) return;

            TagList tags = CacheTags();
            tags.Add(CacheTelemetryNames.AttributePersistenceOperation, operation);
            tags.Add(CacheTelemetryNames.AttributeOutcome, outcome);
            if (errorType != null) tags.Add(CacheTelemetryNames.AttributeErrorType, errorType);

            if (CacheInstrumentation.PersistenceDuration.Enabled)
                CacheInstrumentation.PersistenceDuration.Record(CacheInstrumentation.ElapsedSeconds(startTimestamp), tags);

            if (CacheInstrumentation.PersistenceCalls.Enabled)
                CacheInstrumentation.PersistenceCalls.Add(1, tags);
        }

        /// <summary>
        /// Mark a span as failed and attach an OpenTelemetry exception event. Always returns false so it can be used as an exception filter.
        /// </summary>
        private bool RecordActivityException(Activity activity, Exception exception, string outcome)
        {
            if (activity == null || exception == null) return false;

            try
            {
                string errorType = exception.GetType().FullName;
                activity.SetTag(CacheTelemetryNames.AttributeOutcome, outcome);
                activity.SetTag(CacheTelemetryNames.AttributeErrorType, exception.GetType().Name);
                activity.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);

                if (activity.IsAllDataRequested)
                {
                    ActivityTagsCollection tags = new ActivityTagsCollection();
                    tags["exception.type"] = errorType;
                    if (_Telemetry.RecordExceptionMessages) tags["exception.message"] = exception.Message;
                    if (exception.StackTrace != null) tags["exception.stacktrace"] = exception.StackTrace;
                    activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
                }
            }
            catch (Exception)
            {
                // Best-effort: telemetry must never break the host.
            }

            return false;
        }

        #endregion

    }
}
