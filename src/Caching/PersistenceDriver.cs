using System;
using System.Collections.Generic;
using System.Text;

namespace Caching
{
    /// <summary>
    /// Persistence driver.
    /// </summary>
    public abstract class PersistenceDriver
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
        public abstract void Delete(string key);

        /// <summary>
        /// Retrieve data by its key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Byte data.</returns>
        public abstract byte[] Get(string key);

        /// <summary>
        /// Write data associated with a key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="data">Data.</param>
        public abstract void Write(string key, byte[] data);

        /// <summary>
        /// Check if data exists for a given key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True if exists.</returns>
        public abstract bool Exists(string key);

        /// <summary>
        /// Method to format data into a byte array.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>Byte array.</returns>
        public abstract byte[] ToBytes(object data);

        /// <summary>
        /// Method to convert byte array data into an object.
        /// </summary>
        /// <param name="data">Bytes.</param>
        /// <returns>Instance of type T.</returns>
        public abstract T FromBytes<T>(byte[] data);

        /// <summary>
        /// Enumerate keys.
        /// </summary>
        /// <returns>List of keys.</returns>
        public abstract List<string> Enumerate();

        #endregion

        #region Private-Methods

        #endregion
    }
}
