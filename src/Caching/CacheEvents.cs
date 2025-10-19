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
        public event EventHandler<DataEventArgs<T1, T2>> Added;

        /// <summary>
        /// An item was prepopulated into the cache from persistence.
        /// </summary>
        public event EventHandler<DataEventArgs<T1, T2>> Prepopulated;

        /// <summary>
        /// An item was replaced in the cache.
        /// </summary>
        public event EventHandler<DataEventArgs<T1, T2>> Replaced;

        /// <summary>
        /// An item was removed from the cache.
        /// </summary>
        public event EventHandler<DataEventArgs<T1, T2>> Removed;

        /// <summary>
        /// Items were evicted from the cache.
        /// </summary>
        public event EventHandler<List<T1>> Evicted;

        /// <summary>
        /// Items were expired from the cache.
        /// </summary>
        public event EventHandler<T1> Expired;

        /// <summary>
        /// The cache was cleared.
        /// </summary>
        public event EventHandler Cleared;

        /// <summary>
        /// The cache was disposed.
        /// </summary>
        public event EventHandler Disposed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CacheEvents()
        {
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Invoke Added event.
        /// </summary>
        internal void OnAdded(object sender, DataEventArgs<T1, T2> args) => Added?.Invoke(sender, args);

        /// <summary>
        /// Invoke Prepopulated event.
        /// </summary>
        internal void OnPrepopulated(object sender, DataEventArgs<T1, T2> args) => Prepopulated?.Invoke(sender, args);

        /// <summary>
        /// Invoke Replaced event.
        /// </summary>
        internal void OnReplaced(object sender, DataEventArgs<T1, T2> args) => Replaced?.Invoke(sender, args);

        /// <summary>
        /// Invoke Removed event.
        /// </summary>
        internal void OnRemoved(object sender, DataEventArgs<T1, T2> args) => Removed?.Invoke(sender, args);

        /// <summary>
        /// Invoke Evicted event.
        /// </summary>
        internal void OnEvicted(object sender, List<T1> keys) => Evicted?.Invoke(sender, keys);

        /// <summary>
        /// Invoke Expired event.
        /// </summary>
        internal void OnExpired(object sender, T1 key) => Expired?.Invoke(sender, key);

        /// <summary>
        /// Invoke Cleared event.
        /// </summary>
        internal void OnCleared(object sender, EventArgs args) => Cleared?.Invoke(sender, args);

        /// <summary>
        /// Invoke Disposed event.
        /// </summary>
        internal void OnDisposed(object sender, EventArgs args) => Disposed?.Invoke(sender, args);

        #endregion
    }
}
