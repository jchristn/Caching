namespace Caching
{
    using System;

    /// <summary>
    /// Cache configuration options.
    /// </summary>
    /// <typeparam name="T2">Type of cached values.</typeparam>
    public class CacheOptions<T2>
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of entries.
        /// </summary>
        public int Capacity { get; set; } = 1000;

        /// <summary>
        /// Number of entries to evict when capacity is reached.
        /// </summary>
        public int EvictCount { get; set; } = 10;

        /// <summary>
        /// Maximum memory size in bytes (0 = no limit).
        /// </summary>
        public long MaxMemoryBytes { get; set; } = 0;

        /// <summary>
        /// Function to estimate size of cached values.
        /// </summary>
        public Func<T2, long> SizeEstimator { get; set; } = null;

        /// <summary>
        /// Enable sliding expiration (refresh TTL on access).
        /// </summary>
        public bool SlidingExpiration { get; set; } = false;

        /// <summary>
        /// Expiration check interval in milliseconds.
        /// </summary>
        public int ExpirationIntervalMs { get; set; } = 1000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CacheOptions()
        {
        }

        #endregion
    }
}
