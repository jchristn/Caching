using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Caching
{
    /// <summary>
    /// Cache base class.
    /// </summary>
    public abstract class ICache<T1, T2>
    {
        #region Public-Members

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

        #endregion

        #region Internal-Members

        internal readonly object _CacheLock = new object();
        internal Dictionary<T1, DataNode<T2>> _Cache = new Dictionary<T1, DataNode<T2>>();
        internal IPersistenceDriver<T1, T2> _Persistence = null;
        internal CacheEvents<T1, T2> _Events = new CacheEvents<T1, T2>();

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve the current number of entries in the cache.
        /// </summary>
        /// <returns>An integer containing the number of entries.</returns>
        public abstract int Count();

        /// <summary>
        /// Retrieve the key of the oldest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public abstract T1 Oldest();

        /// <summary>
        /// Retrieve the key of the newest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public abstract T1 Newest();

        /// <summary>
        /// Retrieve all entries from the cache.
        /// </summary>
        /// <returns>Dictionary.</returns>
        public abstract Dictionary<T1, T2> All();

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <returns>The object data associated with the key.</returns>
        public abstract T2 Get(T1 key);

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
        public abstract void AddReplace(T1 key, T2 val);

        /// <summary>
        /// Attempt to add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param> 
        /// <returns>True if successful.</returns>
        public abstract bool TryAddReplace(T1 key, T2 val);

        /// <summary>
        /// Remove a key from the cache.
        /// </summary>
        /// <param name="key">The key.</param> 
        public abstract void Remove(T1 key);

        /// <summary>
        /// Retrieve all keys in the cache.
        /// </summary>
        /// <returns>List of string.</returns>
        public abstract List<T1> GetKeys();

        /// <summary>
        /// Prepopulate the cache with entries from the persistence layer.
        /// </summary>
        public abstract void Prepopulate();

        #endregion

        #region Private-Methods

        #endregion
    }
}
