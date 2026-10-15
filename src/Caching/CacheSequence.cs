namespace Caching
{
    using System.Threading;

    /// <summary>
    /// Process-wide, strictly increasing sequence used to order cache entries by insertion and use.
    /// Wall-clock timestamps cannot be used for ordering because operations within one clock tick tie.
    /// </summary>
    internal static class CacheSequence
    {
        #region Private-Members

        private static long _Current = 0;

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Retrieve the next sequence value.
        /// </summary>
        /// <returns>A value greater than every value previously returned.</returns>
        internal static long Next()
        {
            return Interlocked.Increment(ref _Current);
        }

        #endregion
    }
}
