namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Caching;
    using Touchstone.Core;

    /// <summary>
    /// Shared Touchstone descriptors for the Caching library.
    /// </summary>
    public static class CacheTestSuites
    {
        private static readonly CachePolicy[] Policies = { CachePolicy.Fifo, CachePolicy.Lru };

        /// <summary>
        /// All shared suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    CoreOperationsSuite(),
                    EvictionPolicySuite(),
                    ExpirationSuite(),
                    MemorySuite(),
                    PersistenceAndEventsSuite(),
                    StatisticsSuite(),
                    AsyncAndConcurrencySuite(),
                    DisposalSuite()
                };
            }
        }

        private enum CachePolicy
        {
            Fifo,
            Lru
        }

        private static TestSuiteDescriptor CoreOperationsSuite()
        {
            const string suite = "Core";

            return new TestSuiteDescriptor(
                suite,
                "Core cache operations",
                new List<TestCaseDescriptor>
                {
                    Case(suite, "BasicCrud", "Both cache policies support core CRUD operations", BasicCrudAsync),
                    Case(suite, "ArgumentValidation", "Constructors and public APIs validate invalid inputs", ArgumentValidationAsync),
                    Case(suite, "Comparer", "Custom key comparer treats equivalent keys as one entry", ComparerAsync),
                    Case(suite, "Snapshots", "All() and GetKeys() return isolated snapshots", SnapshotsAsync)
                });
        }

        private static TestSuiteDescriptor EvictionPolicySuite()
        {
            const string suite = "Eviction";

            return new TestSuiteDescriptor(
                suite,
                "Eviction policies",
                new List<TestCaseDescriptor>
                {
                    Case(suite, "FifoOrder", "FIFO eviction removes the oldest inserted entry", FifoEvictsOldestAsync),
                    Case(suite, "LruGetOrder", "LRU eviction respects Get access", LruGetRefreshesUsageAsync),
                    Case(suite, "LruTryGetOrder", "LRU eviction respects TryGet and GetOrDefault access", LruTryGetAndDefaultRefreshUsageAsync),
                    Case(suite, "BulkCapacity", "Bulk inserts stay within capacity and complete within budget", BulkCapacityAsync)
                });
        }

        private static TestSuiteDescriptor ExpirationSuite()
        {
            const string suite = "Expiration";

            return new TestSuiteDescriptor(
                suite,
                "Expiration behavior",
                new List<TestCaseDescriptor>
                {
                    Case(suite, "Absolute", "Absolute expiration removes only expired entries", AbsoluteExpirationAsync),
                    Case(suite, "SlidingBoundedTtl", "Sliding expiration refreshes by the original TTL only", SlidingExpirationDoesNotInflateTtlAsync),
                    Case(suite, "GetOrAddRefreshesSliding", "GetOrAdd refreshes an existing sliding-expiration entry", GetOrAddRefreshesSlidingExpirationAsync)
                });
        }

        private static TestSuiteDescriptor MemorySuite()
        {
            const string suite = "Memory";

            return new TestSuiteDescriptor(
                suite,
                "Memory accounting",
                new List<TestCaseDescriptor>
                {
                    Case(suite, "MutationAccounting", "Memory accounting updates on add, replace, remove, and clear", MemoryAccountingMutationsAsync),
                    Case(suite, "PolicyEviction", "Memory eviction follows each cache policy", MemoryEvictionPoliciesAsync),
                    Case(suite, "ExpirationAccounting", "Expiration decrements tracked memory", ExpirationReducesMemoryAsync),
                    Case(suite, "PrepopulateAccounting", "Prepopulate tracks memory and honors MaxMemoryBytes", PrepopulateTracksMemoryLimitAsync)
                });
        }

        private static TestSuiteDescriptor PersistenceAndEventsSuite()
        {
            const string suite = "PersistenceEvents";

            return new TestSuiteDescriptor(
                suite,
                "Persistence and events",
                new List<TestCaseDescriptor>
                {
                    Case(suite, "PersistenceMutations", "Persistence receives writes, deletes, and clears", PersistenceWritesDeletesClearsAsync),
                    Case(suite, "Prepopulate", "Prepopulate loads up to capacity and raises events", PrepopulateLoadsCapacityAndRaisesEventsAsync),
                    Case(suite, "EvictExpirePersistence", "Eviction and expiration delete persisted values", EvictionAndExpirationDeletePersistenceAsync),
                    Case(suite, "EventPayloads", "Events expose coherent payloads for cache mutations", EventsPayloadsAsync)
                });
        }

        private static TestSuiteDescriptor StatisticsSuite()
        {
            const string suite = "Statistics";

            return new TestSuiteDescriptor(
                suite,
                "Statistics",
                new List<TestCaseDescriptor>
                {
                    Case(suite, "Counters", "Statistics counters and reset match observed operations", StatisticsCountersAndResetAsync)
                });
        }

        private static TestSuiteDescriptor AsyncAndConcurrencySuite()
        {
            const string suite = "AsyncConcurrency";

            return new TestSuiteDescriptor(
                suite,
                "Async and concurrency",
                new List<TestCaseDescriptor>
                {
                    Case(suite, "AsyncMethods", "Async cache methods preserve values and persistence", AsyncOperationsAsync),
                    Case(suite, "GetOrAddAtomic", "Concurrent GetOrAdd invokes the factory once", ConcurrentGetOrAddFactoryOnceAsync),
                    Case(suite, "GetOrAddAsyncAtomic", "Concurrent GetOrAddAsync invokes the factory once", ConcurrentGetOrAddAsyncFactoryOnceAsync),
                    Case(suite, "AddOrUpdateAtomic", "Concurrent AddOrUpdate keeps every increment", ConcurrentAddOrUpdateKeepsAllIncrementsAsync),
                    Case(suite, "MixedOperations", "Concurrent mixed operations stay bounded and exception-free", ConcurrentMixedOperationsAsync)
                });
        }

        private static TestSuiteDescriptor DisposalSuite()
        {
            const string suite = "Disposal";

            return new TestSuiteDescriptor(
                suite,
                "Disposal",
                new List<TestCaseDescriptor>
                {
                    Case(suite, "PostDispose", "Dispose is idempotent and post-dispose APIs throw ObjectDisposedException", DisposalAsync)
                });
        }

        private static TestCaseDescriptor Case(
            string suiteId,
            string caseId,
            string displayName,
            Func<CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, executeAsync);
        }

        private static Task BasicCrudAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, string> cache = CreateCache<string, string>(policy, 10, 2))
                {
                    cache.AddReplace("key1", "value1");
                    AssertTrue(cache.Contains("key1"), PolicyMessage(policy, "added key should be present"));
                    AssertEqual("value1", cache.Get("key1"), PolicyMessage(policy, "Get should return added value"));

                    AssertTrue(cache.TryGet("key1", out string tryValue), PolicyMessage(policy, "TryGet should find existing value"));
                    AssertEqual("value1", tryValue, PolicyMessage(policy, "TryGet should return existing value"));
                    AssertFalse(cache.TryGet("missing", out _), PolicyMessage(policy, "TryGet should not find missing value"));

                    cache.AddReplace("key1", "value1-updated");
                    AssertEqual("value1-updated", cache.Get("key1"), PolicyMessage(policy, "AddReplace should replace existing value"));

                    cache.AddReplace("key2", "value2");
                    cache.AddReplace("key3", "value3");
                    AssertEqual(3, cache.Count(), PolicyMessage(policy, "count should include all entries"));

                    Dictionary<string, string> all = cache.All();
                    AssertEqual(3, all.Count, PolicyMessage(policy, "All should include all entries"));
                    AssertEqual("value2", all["key2"], PolicyMessage(policy, "All should include cached values"));

                    List<string> keys = cache.GetKeys();
                    AssertSetEqual(new[] { "key1", "key2", "key3" }, keys, PolicyMessage(policy, "GetKeys should include all keys"));

                    cache.Remove("key2");
                    AssertFalse(cache.Contains("key2"), PolicyMessage(policy, "Remove should delete key"));
                    AssertEqual(2, cache.Count(), PolicyMessage(policy, "Remove should reduce count"));

                    AssertTrue(cache.TryRemove("key3", out string removed), PolicyMessage(policy, "TryRemove should remove existing key"));
                    AssertEqual("value3", removed, PolicyMessage(policy, "TryRemove should return removed value"));
                    AssertFalse(cache.TryRemove("missing", out _), PolicyMessage(policy, "TryRemove should return false for missing key"));

                    AssertEqual("fallback", cache.GetOrDefault("missing", "fallback"), PolicyMessage(policy, "GetOrDefault should return fallback"));

                    cache.Clear();
                    AssertEqual(0, cache.Count(), PolicyMessage(policy, "Clear should empty cache"));
                    AssertThrows<KeyNotFoundException>(() => cache.Oldest(), PolicyMessage(policy, "Oldest should throw on empty cache"));
                    AssertThrows<KeyNotFoundException>(() => cache.Newest(), PolicyMessage(policy, "Newest should throw on empty cache"));
                }
            }

            return Task.CompletedTask;
        }

        private static Task ArgumentValidationAsync(CancellationToken cancellationToken)
        {
            AssertThrows<ArgumentOutOfRangeException>(() => new FIFOCache<string, string>(0, 1), "FIFO rejects zero capacity");
            AssertThrows<ArgumentOutOfRangeException>(() => new FIFOCache<string, string>(1, 0), "FIFO rejects zero evict count");
            AssertThrows<ArgumentOutOfRangeException>(() => new FIFOCache<string, string>(1, 2), "FIFO rejects evict count greater than capacity");
            AssertThrows<ArgumentOutOfRangeException>(() => new LRUCache<string, string>(0, 1), "LRU rejects zero capacity");
            AssertThrows<ArgumentOutOfRangeException>(() => new LRUCache<string, string>(1, 0), "LRU rejects zero evict count");
            AssertThrows<ArgumentOutOfRangeException>(() => new LRUCache<string, string>(1, 2), "LRU rejects evict count greater than capacity");

            using (CacheBase<string, string> cache = CreateCache<string, string>(CachePolicy.Fifo, 3, 1))
            {
                AssertThrows<ArgumentNullException>(() => cache.AddReplace(null, "value"), "AddReplace rejects null key");
                AssertThrows<ArgumentException>(() => cache.AddReplace("past", "value", DateTime.UtcNow.AddSeconds(-1)), "AddReplace rejects past expiration");
                AssertThrows<ArgumentNullException>(() => cache.TryGet(null, out _), "TryGet rejects null key");
                AssertThrows<ArgumentNullException>(() => cache.GetOrAdd("factory", null), "GetOrAdd rejects null factory");
                AssertFalse(cache.TryAddReplace(null, "value"), "TryAddReplace returns false for null key");
                AssertFalse(cache.TryAddReplace("past", "value", DateTime.UtcNow.AddSeconds(-1)), "TryAddReplace returns false for past expiration");
                AssertFalse(cache.TryGetOrAdd(null, key => "value", out _), "TryGetOrAdd returns false for null key");
                AssertFalse(cache.TryRemove(null, out _), "TryRemove returns false for null key before disposal");
            }

            return Task.CompletedTask;
        }

        private static Task ComparerAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, int> cache = CreateCache<string, int>(policy, 10, 2, StringComparer.OrdinalIgnoreCase))
                {
                    cache.AddReplace("User", 1);
                    cache.AddReplace("user", 2);

                    AssertEqual(1, cache.Count(), PolicyMessage(policy, "case-insensitive keys should collapse to one entry"));
                    AssertEqual(2, cache.Get("USER"), PolicyMessage(policy, "case-insensitive lookup should return replaced value"));
                    AssertTrue(object.ReferenceEquals(StringComparer.OrdinalIgnoreCase, cache.KeyComparer), PolicyMessage(policy, "KeyComparer should expose supplied comparer"));
                }
            }

            return Task.CompletedTask;
        }

        private static Task SnapshotsAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, string> cache = CreateCache<string, string>(policy, 10, 2))
                {
                    cache.AddReplace("A", "one");
                    cache.AddReplace("B", "two");

                    Dictionary<string, string> all = cache.All();
                    List<string> keys = cache.GetKeys();
                    all["C"] = "three";
                    keys.Add("C");

                    AssertEqual(2, cache.Count(), PolicyMessage(policy, "snapshot mutation should not alter cache count"));
                    AssertFalse(cache.Contains("C"), PolicyMessage(policy, "snapshot mutation should not add cache entries"));
                }
            }

            return Task.CompletedTask;
        }

        private static async Task FifoEvictsOldestAsync(CancellationToken cancellationToken)
        {
            using (CacheBase<string, string> cache = CreateCache<string, string>(
                CachePolicy.Fifo,
                CacheTestExpectations.SmallCapacity,
                CacheTestExpectations.SmallEvictCount))
            {
                await AddSequentialAsync(cache, cancellationToken, "A", "B", "C", "D");

                AssertSetEqual(CacheTestExpectations.FifoAfterSingleEviction, cache.GetKeys(), "FIFO should retain expected keys after one eviction");
                AssertFalse(cache.Contains("A"), "FIFO should evict oldest inserted key");
                AssertEqual("B", cache.Oldest(), "FIFO oldest key should be the oldest remaining insertion");
                AssertEqual("D", cache.Newest(), "FIFO newest key should be the latest insertion");
            }
        }

        private static async Task LruGetRefreshesUsageAsync(CancellationToken cancellationToken)
        {
            using (LRUCache<string, string> cache = new LRUCache<string, string>(
                CacheTestExpectations.SmallCapacity,
                CacheTestExpectations.SmallEvictCount))
            {
                await AddSequentialAsync(cache, cancellationToken, "A", "B", "C");
                cache.Get("A");
                await Task.Delay(10, cancellationToken);
                cache.AddReplace("D", "value-D");

                AssertSetEqual(CacheTestExpectations.LruAfterAccessEviction, cache.GetKeys(), "LRU should retain expected keys after Get refresh");
                AssertFalse(cache.Contains("B"), "LRU should evict the least recently used key");
            }
        }

        private static async Task LruTryGetAndDefaultRefreshUsageAsync(CancellationToken cancellationToken)
        {
            using (LRUCache<string, string> cache = new LRUCache<string, string>(
                CacheTestExpectations.SmallCapacity,
                CacheTestExpectations.SmallEvictCount))
            {
                await AddSequentialAsync(cache, cancellationToken, "A", "B", "C");
                AssertTrue(cache.TryGet("B", out _), "TryGet should find B");
                await Task.Delay(10, cancellationToken);
                AssertEqual("value-C", cache.GetOrDefault("C", "missing"), "GetOrDefault should return C");
                await Task.Delay(10, cancellationToken);
                cache.AddReplace("D", "value-D");

                AssertSetEqual(new[] { "B", "C", "D" }, cache.GetKeys(), "TryGet and GetOrDefault should refresh LRU usage");
                AssertFalse(cache.Contains("A"), "A should be the least recently used entry");
            }
        }

        private static Task BulkCapacityAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<int, string> cache = CreateCache<int, string>(policy, CacheTestExpectations.BulkCapacity, 16))
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    for (int i = 0; i < CacheTestExpectations.BulkInsertCount; i++)
                    {
                        cache.AddReplace(i, "value-" + i);
                    }
                    stopwatch.Stop();

                    AssertTrue(cache.Count() <= CacheTestExpectations.BulkCapacity, PolicyMessage(policy, "bulk insert count should not exceed capacity"));
                    AssertTrue(stopwatch.Elapsed <= CacheTestExpectations.ScaleBudget, PolicyMessage(policy, "bulk inserts exceeded performance budget: " + stopwatch.Elapsed));
                }
            }

            return Task.CompletedTask;
        }

        private static async Task AbsoluteExpirationAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, string> cache = CreateCache<string, string>(policy, 10, 2))
                {
                    cache.ExpirationIntervalMs = (int)CacheTestExpectations.ExpirationPollInterval.TotalMilliseconds;
                    cache.AddReplace("short", "value", DateTime.UtcNow.AddMilliseconds(100));
                    cache.AddReplace("long", "value");

                    await WaitUntilAsync(
                        () => !cache.Contains("short"),
                        CacheTestExpectations.ExpirationTimeout,
                        PolicyMessage(policy, "expired entry was not removed"),
                        cancellationToken);

                    AssertTrue(cache.Contains("long"), PolicyMessage(policy, "non-expiring entry should remain"));
                    AssertEqual(1L, cache.GetStatistics().ExpirationCount, PolicyMessage(policy, "expiration count should match removed entry"));
                }
            }
        }

        private static async Task SlidingExpirationDoesNotInflateTtlAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, string> cache = CreateCache<string, string>(policy, 10, 2))
                {
                    cache.ExpirationIntervalMs = (int)CacheTestExpectations.ExpirationPollInterval.TotalMilliseconds;
                    cache.SlidingExpiration = true;
                    cache.AddReplace("slide", "value", DateTime.UtcNow.AddMilliseconds(180));

                    for (int i = 0; i < 3; i++)
                    {
                        await Task.Delay(50, cancellationToken);
                        AssertEqual("value", cache.Get("slide"), PolicyMessage(policy, "sliding entry should be readable before refreshed TTL"));
                    }

                    await Task.Delay(260, cancellationToken);
                    AssertFalse(cache.Contains("slide"), PolicyMessage(policy, "sliding expiration appears to grow beyond original TTL"));
                }
            }
        }

        private static async Task GetOrAddRefreshesSlidingExpirationAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, string> cache = CreateCache<string, string>(policy, 10, 2))
                {
                    int factoryCalls = 0;
                    cache.ExpirationIntervalMs = (int)CacheTestExpectations.ExpirationPollInterval.TotalMilliseconds;
                    cache.SlidingExpiration = true;
                    cache.AddReplace("item", "cached", DateTime.UtcNow.AddMilliseconds(150));

                    await Task.Delay(75, cancellationToken);
                    string value = cache.GetOrAdd("item", key =>
                    {
                        factoryCalls++;
                        return "created";
                    });

                    AssertEqual("cached", value, PolicyMessage(policy, "GetOrAdd should return existing value"));
                    AssertEqual(0, factoryCalls, PolicyMessage(policy, "GetOrAdd should not call factory for existing value"));

                    await Task.Delay(100, cancellationToken);
                    AssertTrue(cache.Contains("item"), PolicyMessage(policy, "GetOrAdd should refresh sliding expiration"));

                    await WaitUntilAsync(
                        () => !cache.Contains("item"),
                        CacheTestExpectations.ExpirationTimeout,
                        PolicyMessage(policy, "GetOrAdd-refreshed entry did not expire"),
                        cancellationToken);
                }
            }
        }

        private static Task MemoryAccountingMutationsAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, string> cache = CreateCache<string, string>(policy, 10, 2))
                {
                    cache.MaxMemoryBytes = 1000;
                    cache.SizeEstimator = value => value.Length;

                    cache.AddReplace("A", "abc");
                    cache.AddReplace("B", "defg");
                    AssertEqual(7L, cache.CurrentMemoryBytes, PolicyMessage(policy, "memory should include added values"));

                    cache.AddReplace("A", "x");
                    AssertEqual(5L, cache.CurrentMemoryBytes, PolicyMessage(policy, "memory should subtract replaced value"));
                    AssertEqual(5L, cache.GetStatistics().CurrentMemoryBytes, PolicyMessage(policy, "statistics should expose current memory"));

                    cache.Remove("B");
                    AssertEqual(1L, cache.CurrentMemoryBytes, PolicyMessage(policy, "memory should subtract removed value"));

                    cache.Clear();
                    AssertEqual(0L, cache.CurrentMemoryBytes, PolicyMessage(policy, "clear should reset memory"));
                }
            }

            return Task.CompletedTask;
        }

        private static async Task MemoryEvictionPoliciesAsync(CancellationToken cancellationToken)
        {
            using (CacheBase<string, string> fifo = CreateCache<string, string>(CachePolicy.Fifo, 10, 2))
            {
                fifo.MaxMemoryBytes = CacheTestExpectations.MemoryLimitBytes;
                fifo.SizeEstimator = value => value.Length;
                fifo.AddReplace("A", "1111");
                fifo.AddReplace("B", "2222");
                fifo.AddReplace("C", "3333");

                AssertSetEqual(new[] { "B", "C" }, fifo.GetKeys(), "FIFO memory eviction should remove oldest entry");
                AssertTrue(fifo.CurrentMemoryBytes <= CacheTestExpectations.MemoryLimitBytes, "FIFO memory should stay within limit");
            }

            using (LRUCache<string, string> lru = new LRUCache<string, string>(10, 2))
            {
                lru.MaxMemoryBytes = CacheTestExpectations.MemoryLimitBytes;
                lru.SizeEstimator = value => value.Length;
                lru.AddReplace("A", "1111");
                await Task.Delay(10, cancellationToken);
                lru.AddReplace("B", "2222");
                await Task.Delay(10, cancellationToken);
                lru.Get("A");
                await Task.Delay(10, cancellationToken);
                lru.AddReplace("C", "3333");

                AssertSetEqual(new[] { "A", "C" }, lru.GetKeys(), "LRU memory eviction should remove least recently used entry");
                AssertTrue(lru.CurrentMemoryBytes <= CacheTestExpectations.MemoryLimitBytes, "LRU memory should stay within limit");
            }
        }

        private static async Task ExpirationReducesMemoryAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, string> cache = CreateCache<string, string>(policy, 10, 2))
                {
                    cache.MaxMemoryBytes = 1000;
                    cache.SizeEstimator = value => value.Length;
                    cache.ExpirationIntervalMs = (int)CacheTestExpectations.ExpirationPollInterval.TotalMilliseconds;
                    cache.AddReplace("expires", "12345", DateTime.UtcNow.AddMilliseconds(100));
                    AssertEqual(5L, cache.CurrentMemoryBytes, PolicyMessage(policy, "memory should be tracked before expiration"));

                    await WaitUntilAsync(
                        () => cache.CurrentMemoryBytes == 0 && !cache.Contains("expires"),
                        CacheTestExpectations.ExpirationTimeout,
                        PolicyMessage(policy, "expiration did not reduce memory"),
                        cancellationToken);
                }
            }
        }

        private static Task PrepopulateTracksMemoryLimitAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                RecordingPersistence<string, string> persistence = new RecordingPersistence<string, string>();
                persistence.Seed("A", "123456");
                persistence.Seed("B", "abcdef");
                persistence.Seed("C", "zz");

                using (CacheBase<string, string> cache = CreatePersistentCache(policy, 10, 2, persistence))
                {
                    cache.MaxMemoryBytes = CacheTestExpectations.MemoryLimitBytes;
                    cache.SizeEstimator = value => value.Length;
                    cache.Prepopulate();

                    AssertEqual(1, cache.Count(), PolicyMessage(policy, "prepopulate should stop before exceeding memory limit"));
                    AssertEqual(6L, cache.CurrentMemoryBytes, PolicyMessage(policy, "prepopulate should account for loaded memory"));
                    AssertTrue(cache.Contains("A"), PolicyMessage(policy, "prepopulate should load entries in persistence order until bounded"));
                    AssertFalse(cache.Contains("B"), PolicyMessage(policy, "prepopulate should not load entry that exceeds memory limit"));
                }
            }

            return Task.CompletedTask;
        }

        private static Task PersistenceWritesDeletesClearsAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                RecordingPersistence<string, string> persistence = new RecordingPersistence<string, string>();

                using (CacheBase<string, string> cache = CreatePersistentCache(policy, 10, 2, persistence))
                {
                    cache.AddReplace("A", "one");
                    cache.AddReplace("B", "two");
                    AssertTrue(persistence.Exists("A"), PolicyMessage(policy, "persistence should contain added key"));
                    AssertEqual("one", persistence.Read("A"), PolicyMessage(policy, "persistence should store added value"));

                    cache.Remove("A");
                    AssertFalse(persistence.Exists("A"), PolicyMessage(policy, "persistence should delete removed key"));

                    cache.Clear();
                    AssertEqual(0, persistence.Count, PolicyMessage(policy, "clear should clear persistence"));
                    AssertEqual(1, persistence.ClearCount, PolicyMessage(policy, "clear should call persistence once"));
                }
            }

            return Task.CompletedTask;
        }

        private static Task PrepopulateLoadsCapacityAndRaisesEventsAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                RecordingPersistence<string, string> persistence = new RecordingPersistence<string, string>();
                persistence.Seed("A", "one");
                persistence.Seed("B", "two");
                persistence.Seed("C", "three");
                persistence.Seed("D", "four");

                int prepopulated = 0;
                using (CacheBase<string, string> cache = CreatePersistentCache(policy, 3, 1, persistence))
                {
                    cache.Events.Prepopulated += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Key) && args.Data != null)
                            prepopulated++;
                    };

                    cache.Prepopulate();

                    AssertEqual(3, cache.Count(), PolicyMessage(policy, "prepopulate should honor capacity"));
                    AssertEqual(3, prepopulated, PolicyMessage(policy, "prepopulate should raise one event per loaded entry"));
                    AssertSetEqual(new[] { "A", "B", "C" }, cache.GetKeys(), PolicyMessage(policy, "prepopulate should load expected keys"));
                    AssertFalse(cache.Contains("D"), PolicyMessage(policy, "prepopulate should not exceed capacity"));
                }
            }

            return Task.CompletedTask;
        }

        private static async Task EvictionAndExpirationDeletePersistenceAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                RecordingPersistence<string, string> persistence = new RecordingPersistence<string, string>();

                using (CacheBase<string, string> cache = CreatePersistentCache(policy, 3, 1, persistence))
                {
                    cache.ExpirationIntervalMs = (int)CacheTestExpectations.ExpirationPollInterval.TotalMilliseconds;
                    await AddSequentialAsync(cache, cancellationToken, "A", "B", "C", "D");

                    AssertFalse(persistence.Exists("A"), PolicyMessage(policy, "capacity eviction should delete persisted entry"));
                    AssertTrue(persistence.Exists("D"), PolicyMessage(policy, "newest entry should remain persisted"));

                    cache.AddReplace("expires", "value", DateTime.UtcNow.AddMilliseconds(100));

                    await WaitUntilAsync(
                        () => !persistence.Exists("expires") && !cache.Contains("expires"),
                        CacheTestExpectations.ExpirationTimeout,
                        PolicyMessage(policy, "expiration should delete persisted entry"),
                        cancellationToken);
                }
            }
        }

        private static Task EventsPayloadsAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, string> cache = CreateCache<string, string>(policy, 3, 1))
                {
                    string addedKey = null;
                    string addedValue = null;
                    string replacedValue = null;
                    string removedValue = null;
                    List<string> evicted = null;
                    bool cleared = false;

                    cache.Events.Added += (sender, args) =>
                    {
                        addedKey = args.Key;
                        addedValue = args.Data.Data;
                    };
                    cache.Events.Replaced += (sender, args) => replacedValue = args.Data.Data;
                    cache.Events.Removed += (sender, args) => removedValue = args.Data.Data;
                    cache.Events.Evicted += (sender, keys) => evicted = new List<string>(keys);
                    cache.Events.Cleared += (sender, args) => cleared = true;

                    cache.AddReplace("A", "one");
                    AssertEqual("A", addedKey, PolicyMessage(policy, "Added event should include key"));
                    AssertEqual("one", addedValue, PolicyMessage(policy, "Added event should include value"));

                    cache.AddReplace("A", "two");
                    AssertEqual("one", replacedValue, PolicyMessage(policy, "Replaced event should include previous value"));
                    AssertEqual("two", addedValue, PolicyMessage(policy, "Added event should include replacement value"));

                    cache.Remove("A");
                    AssertEqual("two", removedValue, PolicyMessage(policy, "Removed event should include removed value"));

                    cache.AddReplace("B", "value-B");
                    cache.AddReplace("C", "value-C");
                    cache.AddReplace("D", "value-D");
                    cache.AddReplace("E", "value-E");

                    AssertTrue(evicted != null && evicted.Count == 1, PolicyMessage(policy, "Evicted event should include evicted keys"));
                    cache.Clear();
                    AssertTrue(cleared, PolicyMessage(policy, "Cleared event should fire"));
                }
            }

            return Task.CompletedTask;
        }

        private static Task StatisticsCountersAndResetAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, string> cache = CreateCache<string, string>(policy, 3, 1))
                {
                    cache.AddReplace("A", "one");
                    cache.AddReplace("B", "two");

                    cache.Get("A");
                    AssertTrue(cache.TryGet("B", out _), PolicyMessage(policy, "TryGet should hit"));
                    AssertEqual("fallback", cache.GetOrDefault("missing", "fallback"), PolicyMessage(policy, "GetOrDefault should miss"));
                    AssertThrows<KeyNotFoundException>(() => cache.Get("missing-2"), PolicyMessage(policy, "Get should miss"));

                    CacheStatistics stats = cache.GetStatistics();
                    AssertEqual(2L, stats.HitCount, PolicyMessage(policy, "hit count should match reads"));
                    AssertEqual(2L, stats.MissCount, PolicyMessage(policy, "miss count should match reads"));
                    AssertTrue(Math.Abs(stats.HitRate - 0.5d) < 0.001d, PolicyMessage(policy, "hit rate should be 50 percent"));
                    AssertEqual(2, stats.CurrentCount, PolicyMessage(policy, "statistics should include current count"));
                    AssertEqual(3, stats.Capacity, PolicyMessage(policy, "statistics should include capacity"));

                    cache.AddReplace("C", "three");
                    cache.AddReplace("D", "four");
                    AssertTrue(cache.GetStatistics().EvictionCount > 0, PolicyMessage(policy, "eviction count should increment"));

                    cache.ResetStatistics();
                    stats = cache.GetStatistics();
                    AssertEqual(0L, stats.HitCount, PolicyMessage(policy, "reset should clear hits"));
                    AssertEqual(0L, stats.MissCount, PolicyMessage(policy, "reset should clear misses"));
                    AssertEqual(0L, stats.EvictionCount, PolicyMessage(policy, "reset should clear evictions"));
                    AssertEqual(0L, stats.ExpirationCount, PolicyMessage(policy, "reset should clear expirations"));
                }
            }

            return Task.CompletedTask;
        }

        private static async Task AsyncOperationsAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                RecordingPersistence<string, string> persistence = new RecordingPersistence<string, string>();

                using (CacheBase<string, string> cache = CreatePersistentCache(policy, 10, 2, persistence))
                {
                    await cache.AddReplaceAsync("A", "one", cancellationToken: cancellationToken);
                    AssertTrue(persistence.Exists("A"), PolicyMessage(policy, "AddReplaceAsync should write persistence"));

                    string created = await cache.GetOrAddAsync("B", key => Task.FromResult("two"), cancellationToken: cancellationToken);
                    AssertEqual("two", created, PolicyMessage(policy, "GetOrAddAsync should create missing value"));

                    int factoryCalls = 0;
                    string existing = await cache.GetOrAddAsync(
                        "B",
                        key =>
                        {
                            factoryCalls++;
                            return Task.FromResult("unused");
                        },
                        cancellationToken: cancellationToken);
                    AssertEqual("two", existing, PolicyMessage(policy, "GetOrAddAsync should return existing value"));
                    AssertEqual(0, factoryCalls, PolicyMessage(policy, "GetOrAddAsync should not call factory for existing value"));

                    string updated = await cache.AddOrUpdateAsync(
                        "B",
                        "add",
                        (key, oldValue) => Task.FromResult(oldValue + "-updated"),
                        cancellationToken: cancellationToken);
                    AssertEqual("two-updated", updated, PolicyMessage(policy, "AddOrUpdateAsync should update existing value"));

                    await cache.RemoveAsync("A", cancellationToken);
                    AssertFalse(persistence.Exists("A"), PolicyMessage(policy, "RemoveAsync should delete persistence"));

                    await cache.ClearAsync(cancellationToken);
                    AssertEqual(0, cache.Count(), PolicyMessage(policy, "ClearAsync should empty cache"));
                }
            }
        }

        private static async Task ConcurrentGetOrAddFactoryOnceAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, int> cache = CreateCache<string, int>(policy, 100, 10))
                {
                    int factoryCalls = 0;
                    ManualResetEventSlim start = new ManualResetEventSlim(false);
                    List<Task<int>> tasks = new List<Task<int>>();

                    for (int i = 0; i < 32; i++)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            start.Wait(cancellationToken);
                            return cache.GetOrAdd("shared", key =>
                            {
                                Interlocked.Increment(ref factoryCalls);
                                Thread.Sleep(10);
                                return 42;
                            });
                        }, cancellationToken));
                    }

                    start.Set();
                    int[] values = await Task.WhenAll(tasks);

                    AssertTrue(values.All(value => value == 42), PolicyMessage(policy, "all GetOrAdd callers should receive the cached value"));
                    AssertEqual(1, factoryCalls, PolicyMessage(policy, "GetOrAdd factory should run once under contention"));
                    AssertEqual(1, cache.Count(), PolicyMessage(policy, "GetOrAdd contention should create one entry"));
                }
            }
        }

        private static async Task ConcurrentGetOrAddAsyncFactoryOnceAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, int> cache = CreateCache<string, int>(policy, 100, 10))
                {
                    int factoryCalls = 0;
                    Task<int>[] tasks = Enumerable.Range(0, 32)
                        .Select(_ => cache.GetOrAddAsync(
                            "shared",
                            async key =>
                            {
                                Interlocked.Increment(ref factoryCalls);
                                await Task.Delay(10, cancellationToken);
                                return 84;
                            },
                            cancellationToken: cancellationToken))
                        .ToArray();

                    int[] values = await Task.WhenAll(tasks);

                    AssertTrue(values.All(value => value == 84), PolicyMessage(policy, "all GetOrAddAsync callers should receive the cached value"));
                    AssertEqual(1, factoryCalls, PolicyMessage(policy, "GetOrAddAsync factory should run once under contention"));
                    AssertEqual(1, cache.Count(), PolicyMessage(policy, "GetOrAddAsync contention should create one entry"));
                }
            }
        }

        private static Task ConcurrentAddOrUpdateKeepsAllIncrementsAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<string, int> cache = CreateCache<string, int>(policy, 10, 2))
                {
                    cache.AddReplace("count", 0);

                    Parallel.For(
                        0,
                        CacheTestExpectations.AddOrUpdateIterations,
                        new ParallelOptions { CancellationToken = cancellationToken },
                        _ => cache.AddOrUpdate("count", 1, (key, oldValue) => oldValue + 1));

                    AssertEqual(CacheTestExpectations.AddOrUpdateIterations, cache.Get("count"), PolicyMessage(policy, "AddOrUpdate should not lose concurrent increments"));
                }
            }

            return Task.CompletedTask;
        }

        private static async Task ConcurrentMixedOperationsAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                using (CacheBase<int, int> cache = CreateCache<int, int>(policy, CacheTestExpectations.BulkCapacity, 16))
                {
                    ConcurrentQueue<Exception> exceptions = new ConcurrentQueue<Exception>();
                    List<Task> tasks = new List<Task>();

                    for (int worker = 0; worker < CacheTestExpectations.ConcurrentWorkers; worker++)
                    {
                        int workerId = worker;
                        tasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                for (int i = 0; i < CacheTestExpectations.ConcurrentOperationsPerWorker; i++)
                                {
                                    int key = workerId * 100000 + i;
                                    cache.AddReplace(key, key);
                                    cache.TryGet(key, out _);
                                    cache.GetOrDefault(key + 1, -1);

                                    if (i % 7 == 0)
                                        cache.TryRemove(key, out _);
                                }
                            }
                            catch (Exception ex)
                            {
                                exceptions.Enqueue(ex);
                            }
                        }, cancellationToken));
                    }

                    await Task.WhenAll(tasks);

                    AssertEqual(0, exceptions.Count, PolicyMessage(policy, "concurrent operations should not throw"));
                    AssertTrue(cache.Count() <= cache.Capacity, PolicyMessage(policy, "concurrent operations should not exceed capacity"));
                }
            }
        }

        private static Task DisposalAsync(CancellationToken cancellationToken)
        {
            foreach (CachePolicy policy in Policies)
            {
                CacheBase<string, string> cache = CreateCache<string, string>(policy, 10, 2);
                bool disposed = false;
                cache.Events.Disposed += (sender, args) => disposed = true;
                cache.AddReplace("A", "one");

                cache.Dispose();
                cache.Dispose();

                AssertTrue(disposed, PolicyMessage(policy, "Disposed event should fire"));
                AssertThrows<ObjectDisposedException>(() => cache.Get("A"), PolicyMessage(policy, "Get should throw after dispose"));
                AssertThrows<ObjectDisposedException>(() => cache.TryRemove("A", out _), PolicyMessage(policy, "TryRemove should throw after dispose"));
                AssertThrows<ObjectDisposedException>(() => cache.Count(), PolicyMessage(policy, "Count should throw after dispose"));
                AssertThrows<ObjectDisposedException>(() => cache.All(), PolicyMessage(policy, "All should throw after dispose"));
                AssertThrows<ObjectDisposedException>(() => cache.GetKeys(), PolicyMessage(policy, "GetKeys should throw after dispose"));
                AssertThrows<ObjectDisposedException>(() => cache.Clear(), PolicyMessage(policy, "Clear should throw after dispose"));
                AssertThrows<ObjectDisposedException>(() => cache.Oldest(), PolicyMessage(policy, "Oldest should throw after dispose"));
                AssertThrows<ObjectDisposedException>(() => cache.Newest(), PolicyMessage(policy, "Newest should throw after dispose"));
            }

            return Task.CompletedTask;
        }

        private static CacheBase<TKey, TValue> CreateCache<TKey, TValue>(
            CachePolicy policy,
            int capacity,
            int evictCount,
            IEqualityComparer<TKey> comparer = null)
        {
            if (policy == CachePolicy.Fifo)
                return new FIFOCache<TKey, TValue>(capacity, evictCount, comparer);

            return new LRUCache<TKey, TValue>(capacity, evictCount, comparer);
        }

        private static CacheBase<TKey, TValue> CreatePersistentCache<TKey, TValue>(
            CachePolicy policy,
            int capacity,
            int evictCount,
            IPersistenceDriver<TKey, TValue> persistence,
            IEqualityComparer<TKey> comparer = null)
        {
            if (policy == CachePolicy.Fifo)
                return new FIFOCache<TKey, TValue>(capacity, evictCount, persistence, comparer);

            return new LRUCache<TKey, TValue>(capacity, evictCount, persistence, comparer);
        }

        private static async Task AddSequentialAsync(
            CacheBase<string, string> cache,
            CancellationToken cancellationToken,
            params string[] keys)
        {
            foreach (string key in keys)
            {
                cache.AddReplace(key, "value-" + key);
                await Task.Delay(10, cancellationToken);
            }
        }

        private static async Task WaitUntilAsync(
            Func<bool> predicate,
            TimeSpan timeout,
            string failureMessage,
            CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Exception lastException = null;

            while (stopwatch.Elapsed < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (predicate())
                        return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                await Task.Delay(CacheTestExpectations.ExpirationPollInterval, cancellationToken);
            }

            if (lastException != null)
                throw new InvalidOperationException(failureMessage + " Last exception: " + lastException.Message, lastException);

            throw new InvalidOperationException(failureMessage);
        }

        private static string PolicyMessage(CachePolicy policy, string message)
        {
            return policy + ": " + message;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(message + ". Expected '" + expected + "' but got '" + actual + "'.");
        }

        private static void AssertSetEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
        {
            List<T> expectedList = expected.OrderBy(value => value).ToList();
            List<T> actualList = actual.OrderBy(value => value).ToList();

            if (expectedList.Count != actualList.Count)
            {
                throw new InvalidOperationException(
                    message + ". Expected [" + string.Join(", ", expectedList) + "] but got [" + string.Join(", ", actualList) + "].");
            }

            for (int i = 0; i < expectedList.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(expectedList[i], actualList[i]))
                {
                    throw new InvalidOperationException(
                        message + ". Expected [" + string.Join(", ", expectedList) + "] but got [" + string.Join(", ", actualList) + "].");
                }
            }
        }

        private static void AssertThrows<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    message + ". Expected " + typeof(TException).Name + " but got " + ex.GetType().Name + ".", ex);
            }

            throw new InvalidOperationException(message + ". Expected " + typeof(TException).Name + " but no exception was thrown.");
        }

        private sealed class RecordingPersistence<TKey, TValue> : IPersistenceDriver<TKey, TValue>
        {
            private readonly object _Lock = new object();
            private readonly Dictionary<TKey, TValue> _Values;
            private readonly List<TKey> _Order = new List<TKey>();
            private readonly IEqualityComparer<TKey> _Comparer;

            public RecordingPersistence(IEqualityComparer<TKey> comparer = null)
            {
                _Comparer = comparer ?? EqualityComparer<TKey>.Default;
                _Values = new Dictionary<TKey, TValue>(_Comparer);
            }

            public int ClearCount { get; private set; }

            public int Count
            {
                get
                {
                    lock (_Lock)
                    {
                        return _Values.Count;
                    }
                }
            }

            public void Seed(TKey key, TValue value)
            {
                lock (_Lock)
                {
                    if (!_Values.ContainsKey(key))
                        _Order.Add(key);

                    _Values[key] = value;
                }
            }

            public bool Exists(TKey key)
            {
                lock (_Lock)
                {
                    return _Values.ContainsKey(key);
                }
            }

            public TValue Read(TKey key)
            {
                lock (_Lock)
                {
                    return _Values[key];
                }
            }

            public Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_Lock)
                {
                    if (_Values.Remove(key))
                        _Order.RemoveAll(existing => _Comparer.Equals(existing, key));
                }

                return Task.CompletedTask;
            }

            public Task ClearAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_Lock)
                {
                    ClearCount++;
                    _Values.Clear();
                    _Order.Clear();
                }

                return Task.CompletedTask;
            }

            public Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_Lock)
                {
                    return Task.FromResult(_Values[key]);
                }
            }

            public Task WriteAsync(TKey key, TValue data, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_Lock)
                {
                    if (!_Values.ContainsKey(key))
                        _Order.Add(key);

                    _Values[key] = data;
                }

                return Task.CompletedTask;
            }

            public Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Exists(key));
            }

            public Task<List<TKey>> EnumerateAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_Lock)
                {
                    return Task.FromResult(new List<TKey>(_Order));
                }
            }
        }
    }
}
