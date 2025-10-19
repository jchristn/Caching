using Caching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Test.Automated
{
    class Program
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        static List<TestResult> Results = new List<TestResult>();

        static void Main(string[] args)
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("Caching Library v4.0.0 - Automated Tests");
            Console.WriteLine("==========================================\n");

            // Run all tests
            TestFIFOBasicOperations();
            TestLRUBasicOperations();
            TestAbsoluteExpiration();
            TestSlidingExpiration();
            TestEvents();
            TestPersistence();
            TestStatistics();
            TestGetOrAdd();
            TestAddOrUpdate();
            TestMemoryLimits();
            TestThreadSafety();
            TestDisposal();
            TestEdgeCases();
            TestPerformance();

            // Print summary
            Console.WriteLine("\n==========================================");
            Console.WriteLine("TEST SUMMARY");
            Console.WriteLine("==========================================");

            int passed = Results.Count(r => r.Passed);
            int failed = Results.Count(r => !r.Passed);

            foreach (var result in Results)
            {
                Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")}: {result.TestName}");
                if (!result.Passed && !string.IsNullOrEmpty(result.Message))
                {
                    Console.WriteLine($"      Error: {result.Message}");
                }
            }

            Console.WriteLine($"\nTotal: {Results.Count} | Passed: {passed} | Failed: {failed}");
            Console.WriteLine($"\nOVERALL RESULT: {(failed == 0 ? "PASS" : "FAIL")}");

            Environment.ExitCode = failed > 0 ? 1 : 0;
        }

        static void RecordTest(string name, bool passed, string message = "")
        {
            Results.Add(new TestResult { TestName = name, Passed = passed, Message = message });
            Console.WriteLine($"{(passed ? "[PASS]" : "[FAIL]")} {name}");
            if (!passed && !string.IsNullOrEmpty(message))
            {
                Console.WriteLine($"       {message}");
            }
        }

        static void TestFIFOBasicOperations()
        {
            Console.WriteLine("\n--- FIFO Basic Operations ---");

            try
            {
                var cache = new FIFOCache<string, string>(10, 2);

                // Test Add
                cache.AddReplace("key1", "value1");
                RecordTest("FIFO: Add item", cache.Contains("key1"));

                // Test Get
                string val = cache.Get("key1");
                RecordTest("FIFO: Get item", val == "value1");

                // Test TryGet
                bool found = cache.TryGet("key1", out string tryVal);
                RecordTest("FIFO: TryGet existing", found && tryVal == "value1");

                bool notFound = cache.TryGet("nonexistent", out string _);
                RecordTest("FIFO: TryGet non-existing", !notFound);

                // Test Replace
                cache.AddReplace("key1", "value1_updated");
                RecordTest("FIFO: Replace item", cache.Get("key1") == "value1_updated");

                // Test Count
                cache.AddReplace("key2", "value2");
                cache.AddReplace("key3", "value3");
                RecordTest("FIFO: Count", cache.Count() == 3);

                // Test GetKeys
                var keys = cache.GetKeys();
                RecordTest("FIFO: GetKeys", keys.Count == 3 && keys.Contains("key1"));

                // Test All
                var all = cache.All();
                RecordTest("FIFO: All", all.Count == 3 && all["key2"] == "value2");

                // Test Remove
                cache.Remove("key2");
                RecordTest("FIFO: Remove", !cache.Contains("key2") && cache.Count() == 2);

                // Test TryRemove
                bool removed = cache.TryRemove("key3");
                RecordTest("FIFO: TryRemove existing", removed && !cache.Contains("key3"));

                bool notRemoved = cache.TryRemove("nonexistent");
                RecordTest("FIFO: TryRemove non-existing", !notRemoved);

                // Test Eviction - strict FIFO order validation
                cache.Clear();
                for (int i = 0; i < 15; i++)
                {
                    cache.AddReplace($"evict{i}", $"value{i}");
                }

                // With capacity=10 and evictCount=2, adding 15 items results in 9 items
                // because eviction happens when count >= capacity
                int finalCount = cache.Count();
                RecordTest("FIFO: Capacity respected", finalCount <= 10 && finalCount >= 9);

                // Oldest items should be evicted (FIFO)
                bool fifoOrderCorrect = !cache.Contains("evict0") && !cache.Contains("evict1") &&
                                       !cache.Contains("evict2") && !cache.Contains("evict3") &&
                                       !cache.Contains("evict4") && !cache.Contains("evict5");
                RecordTest("FIFO: Oldest items evicted", fifoOrderCorrect);

                // Most recent items should remain
                bool fifoNewestRemain = cache.Contains("evict13") && cache.Contains("evict14");
                RecordTest("FIFO: Newest items remain", fifoNewestRemain);

                // Verify oldest remaining item is from later in sequence
                string oldestAfterEviction = cache.Oldest();
                int oldestIndex = int.Parse(oldestAfterEviction.Replace("evict", ""));
                RecordTest("FIFO: Oldest after eviction is correct", oldestIndex >= 6);

                // Test Oldest/Newest
                string oldest = cache.Oldest();
                string newest = cache.Newest();
                RecordTest("FIFO: Oldest/Newest", oldest != newest);

                // Test Clear
                cache.Clear();
                RecordTest("FIFO: Clear", cache.Count() == 0);

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("FIFO: Basic Operations", false, ex.ToString());
            }
        }

        static void TestLRUBasicOperations()
        {
            Console.WriteLine("\n--- LRU Basic Operations ---");

            try
            {
                var cache = new LRUCache<string, string>(10, 2);

                // Test basic operations
                cache.AddReplace("key1", "value1");
                RecordTest("LRU: Add item", cache.Contains("key1"));

                string val = cache.Get("key1");
                RecordTest("LRU: Get item", val == "value1");

                cache.AddReplace("key1", "value1_updated");
                RecordTest("LRU: Replace item", cache.Get("key1") == "value1_updated");

                // Test LRU eviction behavior with strict validation
                cache.Clear();
                for (int i = 0; i < 10; i++)
                {
                    cache.AddReplace($"lru{i}", $"value{i}");
                    Thread.Sleep(2); // Ensure different timestamps
                }

                // Access some items to update LastUsed (making them most recently used)
                Thread.Sleep(10);
                cache.Get("lru0");
                cache.Get("lru1");
                Thread.Sleep(10); // Ensure timestamp difference

                // Add 3 more items to trigger eviction (evictCount=2, so 2 items evicted each time)
                cache.AddReplace("lru10", "value10");
                Thread.Sleep(2);
                cache.AddReplace("lru11", "value11");
                Thread.Sleep(2);
                cache.AddReplace("lru12", "value12");

                // lru2 and lru3 should be evicted first (least recently used)
                // lru0 and lru1 were accessed, so they should still be present
                bool lruEvictionCorrect = cache.Contains("lru0") && cache.Contains("lru1") &&
                                         !cache.Contains("lru2") && !cache.Contains("lru3");
                RecordTest("LRU: LRU eviction order", lruEvictionCorrect);

                // Verify recently added items are still present
                bool recentItemsPresent = cache.Contains("lru10") && cache.Contains("lru11") && cache.Contains("lru12");
                RecordTest("LRU: Recently added items remain", recentItemsPresent);

                // Test that Get updates LastUsed
                cache.Clear();
                cache.AddReplace("access1", "val1");
                Thread.Sleep(10);
                cache.AddReplace("access2", "val2");
                Thread.Sleep(10);
                cache.AddReplace("access3", "val3");
                Thread.Sleep(10);

                // Access access1 to make it recently used
                cache.Get("access1");
                Thread.Sleep(10);

                // Fill cache to capacity and beyond
                for (int i = 0; i < 10; i++)
                {
                    cache.AddReplace($"filler{i}", $"fill{i}");
                    Thread.Sleep(2);
                }

                // access1 was accessed most recently before fillers, so might survive
                // access2 and access3 were never accessed after initial add, should be evicted
                bool accessPatternRespected = !cache.Contains("access2") && !cache.Contains("access3");
                RecordTest("LRU: Access pattern affects eviction", accessPatternRespected);

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("LRU: Basic Operations", false, ex.ToString());
            }
        }

        static void TestAbsoluteExpiration()
        {
            Console.WriteLine("\n--- Absolute Expiration ---");

            try
            {
                var cache = new FIFOCache<string, string>(10, 2);
                cache.ExpirationIntervalMs = 100;

                // Wait for initial expiration task cycle to complete (1000ms default interval)
                Thread.Sleep(1100);

                // Add with expiration
                cache.AddReplace("expire1", "value1", DateTime.UtcNow.AddMilliseconds(200));
                cache.AddReplace("expire2", "value2"); // No expiration

                RecordTest("Expiration: Item added with expiration", cache.Contains("expire1"));

                // Wait for expiration
                Thread.Sleep(250);

                // Give expiration task time to run (using 100ms interval now)
                Thread.Sleep(150);

                bool expire1Gone = !cache.Contains("expire1");
                RecordTest("Expiration: Item expired", expire1Gone, expire1Gone ? "" : "Item 'expire1' still exists after expiration time + task interval");

                bool expire2Remains = cache.Contains("expire2");
                RecordTest("Expiration: Non-expiring item remains", expire2Remains, expire2Remains ? "" : "Item 'expire2' was incorrectly removed");

                // Test TimeSpan overload
                cache.AddReplace("expire3", "value3", TimeSpan.FromMilliseconds(150));
                Thread.Sleep(200);
                Thread.Sleep(150); // Wait for expiration task
                bool expire3Gone = !cache.Contains("expire3");
                RecordTest("Expiration: TimeSpan expiration", expire3Gone, expire3Gone ? "" : "Item 'expire3' still exists after TimeSpan expiration");

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("Expiration: Absolute", false, ex.ToString());
            }
        }

        static void TestSlidingExpiration()
        {
            Console.WriteLine("\n--- Sliding Expiration ---");

            try
            {
                var cache = new FIFOCache<string, string>(10, 2);
                cache.SlidingExpiration = true;
                cache.ExpirationIntervalMs = 100;

                // Wait for initial expiration task cycle to complete (1000ms default interval)
                Thread.Sleep(1100);

                cache.AddReplace("sliding1", "value1", DateTime.UtcNow.AddMilliseconds(250));

                // Access item before expiration to refresh
                Thread.Sleep(150);
                cache.Get("sliding1"); // Should refresh expiration

                Thread.Sleep(150); // Original would have expired, but sliding should keep it

                RecordTest("SlidingExp: Item refreshed on access", cache.Contains("sliding1"));

                // Now don't access and let it expire
                Thread.Sleep(300);
                Thread.Sleep(150); // Wait for expiration task

                bool sliding1Gone = !cache.Contains("sliding1");
                RecordTest("SlidingExp: Item expires without access", sliding1Gone, sliding1Gone ? "" : "Item 'sliding1' still exists after sliding expiration without access");

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("SlidingExp: Sliding Expiration", false, ex.ToString());
            }
        }

        static void TestEvents()
        {
            Console.WriteLine("\n--- Events ---");

            try
            {
                var cache = new FIFOCache<string, string>(5, 2);
                cache.ExpirationIntervalMs = 100;

                bool addedFired = false;
                bool replacedFired = false;
                bool removedFired = false;
                bool evictedFired = false;
                bool expiredFired = false;
                bool clearedFired = false;

                cache.Events.Added += (s, e) => addedFired = true;
                cache.Events.Replaced += (s, e) => replacedFired = true;
                cache.Events.Removed += (s, e) => removedFired = true;
                cache.Events.Evicted += (s, e) => evictedFired = true;
                cache.Events.Expired += (s, e) => expiredFired = true;
                cache.Events.Cleared += (s, e) => clearedFired = true;

                cache.AddReplace("event1", "value1");
                RecordTest("Events: Added event", addedFired);

                cache.AddReplace("event1", "value1_updated");
                RecordTest("Events: Replaced event", replacedFired);

                cache.Remove("event1");
                RecordTest("Events: Removed event", removedFired);

                // Trigger eviction
                for (int i = 0; i < 10; i++)
                {
                    cache.AddReplace($"evt{i}", $"value{i}");
                }
                RecordTest("Events: Evicted event", evictedFired);

                // Trigger expiration
                cache.AddReplace("expire_event", "value", DateTime.UtcNow.AddMilliseconds(150));
                Thread.Sleep(200);
                Thread.Sleep(150); // Wait for expiration task
                RecordTest("Events: Expired event", expiredFired);

                cache.Clear();
                RecordTest("Events: Cleared event", clearedFired);

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("Events: Event firing", false, ex.ToString());
            }
        }

        static void TestPersistence()
        {
            Console.WriteLine("\n--- Persistence ---");

            try
            {
                string testDir = Path.Combine(Path.GetTempPath(), "CacheTest_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(testDir);

                var persistence = new SimplePersistence(testDir);
                var cache = new FIFOCache<string, string>(10, 2, persistence);

                cache.AddReplace("persist1", "value1");
                cache.AddReplace("persist2", "value2");

                RecordTest("Persistence: Write on add", persistence.Exists("persist1"));

                // Validate file contents match expected values
                string persistedValue = persistence.Get("persist1");
                RecordTest("Persistence: File contents match", persistedValue == "value1");

                cache.Remove("persist1");
                RecordTest("Persistence: Delete on remove", !persistence.Exists("persist1"));

                cache.Clear();
                RecordTest("Persistence: Clear persistence", !persistence.Exists("persist2"));

                // Test prepopulate
                persistence.Write("pre1", "prevalue1");
                persistence.Write("pre2", "prevalue2");

                var cache2 = new FIFOCache<string, string>(10, 2, persistence);
                cache2.Prepopulate();

                RecordTest("Persistence: Prepopulate count", cache2.Count() == 2);
                RecordTest("Persistence: Prepopulate data", cache2.Contains("pre1") && cache2.Get("pre1") == "prevalue1");

                // Test persistence during eviction
                for (int i = 0; i < 15; i++)
                {
                    cache2.AddReplace($"evict{i}", $"value{i}");
                }

                // Items that should remain (last 10)
                bool evictedFromCache = !cache2.Contains("evict0") && !cache2.Contains("evict1");
                bool evictedFromPersistence = !persistence.Exists("evict0") && !persistence.Exists("evict1");
                bool remainsInPersistence = persistence.Exists("evict14") && persistence.Get("evict14") == "value14";
                RecordTest("Persistence: Eviction removes from storage", evictedFromCache && evictedFromPersistence);
                RecordTest("Persistence: Non-evicted items persisted", remainsInPersistence);

                // Test persistence with expiration
                var cache3 = new FIFOCache<string, string>(10, 2, persistence);
                cache3.ExpirationIntervalMs = 100;
                Thread.Sleep(150); // Wait for initial expiration task

                bool expiredEventFired = false;
                cache3.Events.Expired += (s, key) => expiredEventFired = true;

                cache3.AddReplace("expire_persist", "expire_value", DateTime.UtcNow.AddMilliseconds(150));
                RecordTest("Persistence: Expired item persisted initially", persistence.Exists("expire_persist"));

                Thread.Sleep(200); // Wait for expiration
                Thread.Sleep(150); // Wait for expiration task

                bool expiredFromPersistence = !persistence.Exists("expire_persist");
                RecordTest("Persistence: Expired item removed from storage", expiredEventFired && expiredFromPersistence);

                // Test concurrent persistence operations
                var cache4 = new FIFOCache<string, string>(100, 10, persistence);
                bool persistenceConcurrentError = false;
                var tasks = new List<Task>();

                for (int t = 0; t < 5; t++)
                {
                    int threadId = t;
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            for (int i = 0; i < 20; i++)
                            {
                                string key = $"concurrent_{threadId}_{i}";
                                cache4.AddReplace(key, $"value_{threadId}_{i}");
                                if (i % 5 == 0) cache4.TryRemove(key);
                            }
                        }
                        catch
                        {
                            persistenceConcurrentError = true;
                        }
                    }));
                }

                Task.WaitAll(tasks.ToArray());
                RecordTest("Persistence: Concurrent operations handled", !persistenceConcurrentError);

                // Verify persistence integrity after concurrent operations
                int validatedCount = 0;
                foreach (var key in cache4.GetKeys())
                {
                    if (persistence.Exists(key) && persistence.Get(key) == cache4.Get(key))
                    {
                        validatedCount++;
                    }
                }
                RecordTest("Persistence: Data integrity after concurrent ops", validatedCount == cache4.Count());

                cache.Dispose();
                cache2.Dispose();
                cache3.Dispose();
                cache4.Dispose();

                // Cleanup
                Directory.Delete(testDir, true);
            }
            catch (Exception ex)
            {
                RecordTest("Persistence: Persistence layer", false, ex.ToString());
            }
        }

        static void TestStatistics()
        {
            Console.WriteLine("\n--- Statistics ---");

            try
            {
                var cache = new FIFOCache<string, string>(10, 2);

                cache.AddReplace("stat1", "value1");
                cache.AddReplace("stat2", "value2");

                cache.Get("stat1"); // Hit
                cache.TryGet("stat2", out _); // Hit
                cache.TryGet("nonexistent", out _); // Miss

                try { cache.Get("nonexistent2"); } catch { } // Miss

                var stats = cache.GetStatistics();

                RecordTest("Stats: Hit count", stats.HitCount == 2);
                RecordTest("Stats: Miss count", stats.MissCount == 2);
                RecordTest("Stats: Hit rate", Math.Abs(stats.HitRate - 0.5) < 0.01);
                RecordTest("Stats: Current count", stats.CurrentCount == 2);
                RecordTest("Stats: Capacity", stats.Capacity == 10);

                // Trigger eviction
                for (int i = 0; i < 15; i++)
                {
                    cache.AddReplace($"evict{i}", $"value{i}");
                }

                stats = cache.GetStatistics();
                RecordTest("Stats: Eviction count", stats.EvictionCount > 0);

                cache.ResetStatistics();
                stats = cache.GetStatistics();
                RecordTest("Stats: Reset statistics", stats.HitCount == 0 && stats.MissCount == 0);

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("Stats: Statistics tracking", false, ex.ToString());
            }
        }

        static void TestGetOrAdd()
        {
            Console.WriteLine("\n--- GetOrAdd ---");

            try
            {
                var cache = new FIFOCache<string, string>(10, 2);

                int factoryCalls = 0;
                string result = cache.GetOrAdd("goa1", key =>
                {
                    factoryCalls++;
                    return "generated_" + key;
                });

                RecordTest("GetOrAdd: Factory called for new item", factoryCalls == 1 && result == "generated_goa1");

                string result2 = cache.GetOrAdd("goa1", key =>
                {
                    factoryCalls++;
                    return "should_not_call";
                });

                RecordTest("GetOrAdd: Factory not called for existing", factoryCalls == 1 && result2 == "generated_goa1");

                // Test with TimeSpan
                cache.GetOrAdd("goa2", key => "value2", TimeSpan.FromSeconds(10));
                RecordTest("GetOrAdd: TimeSpan overload", cache.Contains("goa2"));

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("GetOrAdd: GetOrAdd pattern", false, ex.ToString());
            }
        }

        static void TestAddOrUpdate()
        {
            Console.WriteLine("\n--- AddOrUpdate ---");

            try
            {
                var cache = new FIFOCache<string, string>(10, 2);

                string result = cache.AddOrUpdate("aou1", "initial", (key, old) => "updated_" + old);
                RecordTest("AddOrUpdate: Add new", result == "initial" && cache.Get("aou1") == "initial");

                string result2 = cache.AddOrUpdate("aou1", "should_not_use", (key, old) => "updated_" + old);
                RecordTest("AddOrUpdate: Update existing", result2 == "updated_initial");

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("AddOrUpdate: AddOrUpdate pattern", false, ex.ToString());
            }
        }

        static void TestMemoryLimits()
        {
            Console.WriteLine("\n--- Memory Limits ---");

            try
            {
                var cache = new FIFOCache<string, string>(1000, 10);
                cache.MaxMemoryBytes = 1000; // 1KB limit
                cache.SizeEstimator = str => str.Length * 2; // Unicode estimation

                // Add large item
                string largeValue = new string('X', 400); // ~800 bytes
                cache.AddReplace("mem1", largeValue);

                RecordTest("Memory: Large item added", cache.Contains("mem1"));
                RecordTest("Memory: Memory tracked", cache.CurrentMemoryBytes > 0);

                // Add another that should trigger memory-based eviction
                string largeValue2 = new string('Y', 200); // ~400 bytes
                cache.AddReplace("mem2", largeValue2);

                // First item should be evicted due to memory limit
                bool memoryEviction = !cache.Contains("mem1") && cache.Contains("mem2");
                RecordTest("Memory: Memory-based eviction", memoryEviction);

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("Memory: Memory limits", false, ex.ToString());
            }
        }

        static void TestThreadSafety()
        {
            Console.WriteLine("\n--- Thread Safety ---");

            try
            {
                var cache = new FIFOCache<int, int>(1000, 10);
                int numThreads = 10;
                int opsPerThread = 100;
                bool errorOccurred = false;

                var tasks = new List<Task>();
                for (int t = 0; t < numThreads; t++)
                {
                    int threadId = t;
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            for (int i = 0; i < opsPerThread; i++)
                            {
                                int key = threadId * 1000 + i;
                                cache.AddReplace(key, key * 2);
                                cache.TryGet(key, out _);
                                if (i % 10 == 0) cache.TryRemove(key);
                            }
                        }
                        catch
                        {
                            errorOccurred = true;
                        }
                    }));
                }

                Task.WaitAll(tasks.ToArray());

                RecordTest("ThreadSafety: Concurrent operations", !errorOccurred);
                RecordTest("ThreadSafety: Cache integrity maintained", cache.Count() >= 0 && cache.Count() <= cache.Capacity);

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("ThreadSafety: Thread safety", false, ex.ToString());
            }
        }

        static void TestDisposal()
        {
            Console.WriteLine("\n--- Disposal ---");

            try
            {
                bool disposedEventFired = false;

                var cache = new FIFOCache<string, string>(10, 2);
                cache.Events.Disposed += (s, e) => disposedEventFired = true;

                cache.AddReplace("disp1", "value1");
                cache.Dispose();

                RecordTest("Disposal: Disposed event fired", disposedEventFired);

                // Try to use after disposal - should throw ObjectDisposedException
                bool threwCorrectException = false;
                try
                {
                    cache.Get("disp1");
                }
                catch (ObjectDisposedException)
                {
                    threwCorrectException = true;
                }
                catch { }

                RecordTest("Disposal: ObjectDisposedException on post-dispose usage", threwCorrectException);
            }
            catch (Exception ex)
            {
                RecordTest("Disposal: Proper disposal", false, ex.ToString());
            }
        }

        static void TestEdgeCases()
        {
            Console.WriteLine("\n--- Edge Cases ---");

            try
            {
                var cache = new FIFOCache<string, string>(10, 2);

                // Null key
                bool nullKeyThrows = false;
                try
                {
                    cache.AddReplace(null, "value");
                }
                catch (ArgumentNullException)
                {
                    nullKeyThrows = true;
                }
                RecordTest("EdgeCase: Null key throws", nullKeyThrows);

                // Past expiration
                bool pastExpirationThrows = false;
                try
                {
                    cache.AddReplace("past", "value", DateTime.UtcNow.AddSeconds(-10));
                }
                catch (ArgumentException)
                {
                    pastExpirationThrows = true;
                }
                RecordTest("EdgeCase: Past expiration throws", pastExpirationThrows);

                // TryGet with null key
                bool tryGetNullThrows = false;
                try
                {
                    cache.TryGet(null, out _);
                }
                catch (ArgumentNullException)
                {
                    tryGetNullThrows = true;
                }
                RecordTest("EdgeCase: TryGet null key throws", tryGetNullThrows);

                // TryRemove with null key
                bool tryRemoveNull = cache.TryRemove(null);
                RecordTest("EdgeCase: TryRemove null key returns false", !tryRemoveNull);

                // Remove non-existent (should not fire event)
                bool removedFired = false;
                cache.Events.Removed += (s, e) => removedFired = true;
                cache.Remove("nonexistent_edge");
                RecordTest("EdgeCase: Remove non-existent doesn't fire event", !removedFired);

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("EdgeCase: Edge cases", false, ex.ToString());
            }
        }

        static void TestPerformance()
        {
            Console.WriteLine("\n--- Performance ---");

            try
            {
                var cache = new FIFOCache<int, string>(10000, 100);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Test add performance
                for (int i = 0; i < 10000; i++)
                {
                    cache.AddReplace(i, $"value{i}");
                }
                sw.Stop();

                bool addPerf = sw.ElapsedMilliseconds < 1000; // Should be fast
                RecordTest($"Performance: Add 10k items ({sw.ElapsedMilliseconds}ms)", addPerf);

                // Test get performance (should be O(1))
                sw.Restart();
                for (int i = 0; i < 5000; i++)
                {
                    cache.TryGet(i, out _);
                }
                sw.Stop();

                bool getPerf = sw.ElapsedMilliseconds < 100; // Should be very fast
                RecordTest($"Performance: Get 5k items ({sw.ElapsedMilliseconds}ms)", getPerf);

                cache.Dispose();
            }
            catch (Exception ex)
            {
                RecordTest("Performance: Performance test", false, ex.ToString());
            }
        }
    }

    class TestResult
    {
        public string TestName { get; set; }
        public bool Passed { get; set; }
        public string Message { get; set; }
    }

    // Simple in-memory persistence for testing
    class SimplePersistence : IPersistenceDriver<string, string>
    {
        private Dictionary<string, string> _store = new Dictionary<string, string>();
        private string _directory;

        public SimplePersistence(string directory)
        {
            _directory = directory;
        }

        public void Delete(string key)
        {
            string path = Path.Combine(_directory, key);
            if (File.Exists(path)) File.Delete(path);
        }

        public void Clear()
        {
            if (Directory.Exists(_directory))
            {
                foreach (var file in Directory.GetFiles(_directory))
                {
                    File.Delete(file);
                }
            }
        }

        public string Get(string key)
        {
            string path = Path.Combine(_directory, key);
            return File.ReadAllText(path);
        }

        public void Write(string key, string data)
        {
            string path = Path.Combine(_directory, key);
            File.WriteAllText(path, data);
        }

        public bool Exists(string key)
        {
            string path = Path.Combine(_directory, key);
            return File.Exists(path);
        }

        public List<string> Enumerate()
        {
            if (!Directory.Exists(_directory)) return new List<string>();

            return Directory.GetFiles(_directory)
                .Select(f => Path.GetFileName(f))
                .ToList();
        }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
