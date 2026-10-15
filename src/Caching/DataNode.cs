namespace Caching
{
    using System;

    /// <summary>
    /// Data node.
    /// </summary>
    /// <typeparam name="T">Type of data.</typeparam>
    public class DataNode<T>
    {
        #region Public-Members

        /// <summary>
        /// Data.
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// Time when added.
        /// Informational only; FIFO ordering uses an internal sequence so entries added within the same clock tick stay ordered.
        /// </summary>
        public DateTime Added { get; set; }

        /// <summary>
        /// Time of last use.
        /// Informational only; LRU ordering uses an internal sequence so entries used within the same clock tick stay ordered.
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Timestamp indicating when the entry should be expired.
        /// </summary>
        public DateTime? Expiration { get; set; } = null;

        /// <summary>
        /// Original expiration duration used when sliding expiration refreshes an entry.
        /// </summary>
        public TimeSpan? ExpirationDuration { get; set; } = null;

        #endregion

        #region Internal-Members

        /// <summary>
        /// Insertion order. Used for FIFO eviction, Oldest, and Newest.
        /// </summary>
        internal long AddedSequence { get; set; }

        /// <summary>
        /// Use order. Used for LRU eviction.
        /// </summary>
        internal long LastUsedSequence { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DataNode()
        {
            DateTime ts = DateTime.UtcNow;
            Added = ts;
            LastUsed = ts;
            AddedSequence = CacheSequence.Next();
            LastUsedSequence = AddedSequence;
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="val">Value.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        public DataNode(T val, DateTime? expiration = null)
        {
            DateTime ts = DateTime.UtcNow;
            Added = ts;
            LastUsed = ts;
            AddedSequence = CacheSequence.Next();
            LastUsedSequence = AddedSequence;
            Data = val;
            Expiration = expiration;
            if (expiration.HasValue)
            {
                ExpirationDuration = expiration.Value - ts;
            }
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
