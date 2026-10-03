namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Caching;

    /// <summary>
    /// In-memory persistence driver whose individual operations can be made to fail, used by telemetry tests.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    public sealed class FaultablePersistence<TKey, TValue> : IPersistenceDriver<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> _Values = new ConcurrentDictionary<TKey, TValue>();

        /// <summary>
        /// Throw IOException from WriteAsync.
        /// </summary>
        public bool FailWrite { get; set; }

        /// <summary>
        /// Throw IOException from DeleteAsync.
        /// </summary>
        public bool FailDelete { get; set; }

        /// <summary>
        /// Seed a value without going through the cache.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="value">Value.</param>
        public void Seed(TKey key, TValue value)
        {
            _Values[key] = value;
        }

        /// <inheritdoc />
        public Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
        {
            if (FailDelete) throw new IOException("Simulated delete failure for a secret-key-123.");
            _Values.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _Values.Clear();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_Values[key]);
        }

        /// <inheritdoc />
        public Task WriteAsync(TKey key, TValue data, CancellationToken cancellationToken = default)
        {
            if (FailWrite) throw new IOException("Simulated write failure for a secret-key-123.");
            _Values[key] = data;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_Values.ContainsKey(key));
        }

        /// <inheritdoc />
        public Task<List<TKey>> EnumerateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_Values.Keys.ToList());
        }
    }
}
