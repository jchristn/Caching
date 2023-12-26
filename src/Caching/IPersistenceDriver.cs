namespace Caching
{
    using System.Collections.Generic;

    /// <summary>
    /// Persistence driver.
    /// </summary>
    public abstract class IPersistenceDriver<T1, T2>
    {
        #region Public-Members

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        /// <summary>
        /// Delete data by its key.
        /// </summary>
        /// <param name="key">Key.</param>
        public abstract void Delete(T1 key);

        /// <summary>
        /// Delete all persisted contents.
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// Retrieve data by its key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Data.</returns>
        public abstract T2 Get(T1 key);

        /// <summary>
        /// Write data associated with a key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="data">Data.</param>
        public abstract void Write(T1 key, T2 data);

        /// <summary>
        /// Check if data exists for a given key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True if exists.</returns>
        public abstract bool Exists(T1 key);

        /// <summary>
        /// Enumerate keys.
        /// </summary>
        /// <returns>List of keys.</returns>
        public abstract List<T1> Enumerate();

        #endregion

        #region Private-Methods

        #endregion
    }
}
