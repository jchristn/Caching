namespace Caching
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Async persistence driver interface.
    /// </summary>
    /// <typeparam name="T1">Type of key.</typeparam>
    /// <typeparam name="T2">Type of value.</typeparam>
    public interface IPersistenceDriverAsync<T1, T2>
    {
        /// <summary>
        /// Delete data by its key asynchronously.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(T1 key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete all persisted contents asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieve data by its key asynchronously.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Data.</returns>
        Task<T2> GetAsync(T1 key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write data associated with a key asynchronously.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="data">Data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task WriteAsync(T1 key, T2 data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if data exists for a given key asynchronously.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsAsync(T1 key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate keys asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of keys.</returns>
        Task<List<T1>> EnumerateAsync(CancellationToken cancellationToken = default);
    }
}
