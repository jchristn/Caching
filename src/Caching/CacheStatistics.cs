namespace Caching
{
    /// <summary>
    /// Cache statistics snapshot.
    /// </summary>
    public class CacheStatistics
    {
        #region Public-Members

        /// <summary>
        /// Total number of cache hits.
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Total number of cache misses.
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Total number of evicted entries.
        /// </summary>
        public long EvictionCount { get; set; }

        /// <summary>
        /// Total number of expired entries.
        /// </summary>
        public long ExpirationCount { get; set; }

        /// <summary>
        /// Cache hit rate (0.0 to 1.0).
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Current number of entries in the cache.
        /// </summary>
        public int CurrentCount { get; set; }

        /// <summary>
        /// Cache capacity.
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// Current estimated memory usage in bytes (if memory tracking is enabled).
        /// </summary>
        public long CurrentMemoryBytes { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CacheStatistics()
        {
        }

        #endregion
    }
}
