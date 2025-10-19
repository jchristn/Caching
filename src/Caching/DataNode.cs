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
        /// </summary>
        public DateTime Added { get; set; }

        /// <summary>
        /// Time of last use.
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Timestamp indicating when the entry should be expired.
        /// </summary>
        public DateTime? Expiration { get; set; } = null;

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
            Data = val;
            Expiration = expiration;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
