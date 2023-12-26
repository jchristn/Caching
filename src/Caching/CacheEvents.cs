namespace Caching
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Cache events.
    /// </summary>
    public class CacheEvents<T1, T2>
    {
        #region Public-Members

        /// <summary>
        /// An item was added to the cache.
        /// </summary>
        public EventHandler<DataEventArgs<T1, T2>> Added;

        /// <summary>
        /// An item was prepopulated into the cache from persistence.
        /// </summary>
        public EventHandler<DataEventArgs<T1, T2>> Prepopulated;

        /// <summary>
        /// An item was replaced in the cache.
        /// </summary>
        public EventHandler<DataEventArgs<T1, T2>> Replaced;

        /// <summary>
        /// An item was removed from the cache.
        /// </summary>
        public EventHandler<DataEventArgs<T1, T2>> Removed;

        /// <summary>
        /// Items were evicted from the cache.
        /// </summary>
        public EventHandler<List<T1>> Evicted;

        /// <summary>
        /// Items were expired from the cache.
        /// </summary>
        public EventHandler<T1> Expired;

        /// <summary>
        /// The cache was cleared.
        /// </summary>
        public EventHandler Cleared;

        /// <summary>
        /// The cache was disposed.
        /// </summary>
        public EventHandler Disposed;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CacheEvents()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
