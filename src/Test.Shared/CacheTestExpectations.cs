namespace Test.Shared
{
    using System;

    /// <summary>
    /// Central expected values used by the shared cache test descriptors.
    /// </summary>
    public static class CacheTestExpectations
    {
        public const int SmallCapacity = 3;
        public const int SmallEvictCount = 1;
        public const int BulkCapacity = 128;
        public const int BulkInsertCount = 2000;
        public const int ConcurrentWorkers = 8;
        public const int ConcurrentOperationsPerWorker = 250;
        public const int AddOrUpdateIterations = 500;
        public const long MemoryLimitBytes = 10;
        public static readonly TimeSpan ExpirationPollInterval = TimeSpan.FromMilliseconds(25);
        public static readonly TimeSpan ExpirationTimeout = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan ScaleBudget = TimeSpan.FromSeconds(5);
        public static readonly string[] FifoAfterSingleEviction = { "B", "C", "D" };
        public static readonly string[] LruAfterAccessEviction = { "A", "C", "D" };
    }
}
