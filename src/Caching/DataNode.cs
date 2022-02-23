using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Caching
{
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

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DataNode()
        {
            DateTime ts = DateTime.Now;
            Added = ts;
            LastUsed = ts;
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="val">Value.</param>
        public DataNode(T val)
        {
            DateTime ts = DateTime.Now;
            Added = ts;
            LastUsed = ts;
            Data = val;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
