namespace Caching
{
    using System;

    /// <summary>
    /// Data event arguments.
    /// </summary>
    public class DataEventArgs<T1, T2> : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Key.
        /// </summary>
        public T1 Key { get; }

        /// <summary>
        /// Data.
        /// </summary>
        public DataNode<T2> Data { get; }

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="data">Data.</param>
        public DataEventArgs(T1 key, DataNode<T2> data)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));

            Key = key;
            Data = data;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
