namespace Caching
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Persistence driver interface. Implement this interface for custom persistence.
    /// </summary>
    /// <typeparam name="T1">Type of key.</typeparam>
    /// <typeparam name="T2">Type of value.</typeparam>
    public interface IPersistenceDriver<T1, T2>
    {

        /// <summary>
        /// Delete data by its key.
        /// </summary>
        /// <param name="key">Key.</param>
        void Delete(T1 key);

        /// <summary>
        /// Delete all persisted contents.
        /// </summary>
        void Clear();

        /// <summary>
        /// Retrieve data by its key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Data.</returns>
        T2 Get(T1 key);

        /// <summary>
        /// Write data associated with a key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="data">Data.</param>
        void Write(T1 key, T2 data);

        /// <summary>
        /// Check if data exists for a given key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True if exists.</returns>
        bool Exists(T1 key);

        /// <summary>
        /// Enumerate keys.
        /// </summary>
        /// <returns>List of keys.</returns>
        List<T1> Enumerate();
    }

    /// <summary>
    /// Persistence driver base class for backward compatibility.
    /// New code should implement IPersistenceDriver interface directly.
    /// </summary>
    /// <typeparam name="T1">Type of key.</typeparam>
    /// <typeparam name="T2">Type of value.</typeparam>
    [Obsolete("Use IPersistenceDriver<T1, T2> interface directly. This class will be removed in v5.0.")]
    public abstract class PersistenceDriverBase<T1, T2> : IPersistenceDriver<T1, T2>
    {
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
    }
}
