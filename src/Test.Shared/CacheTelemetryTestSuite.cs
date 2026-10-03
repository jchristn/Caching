namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Caching;
    using Touchstone.Core;

    using N = Caching.CacheTelemetryNames;

    /// <summary>
    /// Touchstone suite proving that the Caching library emits its documented metrics and spans.
    /// Each case uses a unique cache name and a listener filtered on it, so cases are isolated from each other.
    /// </summary>
    public static class CacheTelemetryTestSuite
    {
        private const string SuiteId = "Telemetry";
        private static readonly ActivitySource _TestSource = new ActivitySource(TelemetryCapture.TestSourceName);

        /// <summary>
        /// The telemetry suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                SuiteId,
                "Telemetry",
                new List<TestCaseDescriptor>
                {
                    Case("Names", "Meter and activity source names are stable", NamesAsync),
                    Case("NoListener", "Operations and failure paths work with no listener attached", NoListenerAsync),
                    Case("Disabled", "Telemetry.Enable=false suppresses metrics and spans for that cache", DisabledAsync),
                    Case("OperationMetrics", "Operation duration histogram carries operation and outcome for every public operation", OperationMetricsAsync),
                    Case("LookupCounters", "Lookup counter records hits and misses", LookupCountersAsync),
                    Case("EvictionCounters", "Eviction counter records capacity and memory evictions", EvictionCountersAsync),
                    Case("OperationSpans", "Mutations produce spans; lookups only when TraceLookups is set", OperationSpansAsync),
                    Case("WorkflowStages", "GetOrAdd and AddOrUpdate produce lock wait and value factory stage spans and lock metrics", WorkflowStagesAsync),
                    Case("ValueFactoryFailure", "A throwing value factory marks spans and metrics as errors without leaking the message", ValueFactoryFailureAsync),
                    Case("Cancellation", "A canceled lock wait is recorded as canceled, not as an error", CancellationAsync),
                    Case("Persistence", "Persistence calls produce client spans, counters, and duration histograms", PersistenceAsync),
                    Case("PersistenceFailure", "A failing persistence driver is recorded by operation, persistence, and error metrics", PersistenceFailureAsync),
                    Case("TryFailuresRecorded", "Failures swallowed by Try methods are still recorded as errors", TryFailuresRecordedAsync),
                    Case("Prepopulate", "Prepopulate records loaded entries and persistence calls", PrepopulateAsync),
                    Case("ContextPropagation", "Cache spans nest under the caller's span and persistence spans nest under cache spans", ContextPropagationAsync),
                    Case("ExpirationSweep", "Expiration sweeps record counters, duration, last success, and a linked root span", ExpirationSweepAsync),
                    Case("ExpirationFailure", "A failing expiration sweep is recorded as an expiration error", ExpirationFailureAsync),
                    Case("Gauges", "Size, capacity, memory, config, and build info gauges report live caches only", GaugesAsync)
                });
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(SuiteId, caseId, displayName, executeAsync);
        }

        private static string UniqueName()
        {
            return "t-" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private static IEnumerable<CacheBase<string, string>> BothPolicies(string name, int capacity = 10, int evictCount = 2, IPersistenceDriver<string, string> persistence = null)
        {
            CacheBase<string, string> fifo = persistence == null
                ? new FIFOCache<string, string>(capacity, evictCount)
                : new FIFOCache<string, string>(capacity, evictCount, persistence);
            fifo.Name = name;
            yield return fifo;

            CacheBase<string, string> lru = persistence == null
                ? new LRUCache<string, string>(capacity, evictCount)
                : new LRUCache<string, string>(capacity, evictCount, persistence);
            lru.Name = name;
            yield return lru;
        }

        private static Task NamesAsync(CancellationToken token)
        {
            AssertEqual("Caching", N.MeterName, "meter name");
            AssertEqual("Caching", N.ActivitySourceName, "activity source name");
            AssertEqual("caching.operation.duration", N.OperationDuration, "operation duration name");

            using (FIFOCache<string, string> cache = new FIFOCache<string, string>(10, 1))
            {
                AssertEqual(N.DefaultCacheName, cache.Name, "default name");
                cache.Name = null;
                AssertEqual(N.DefaultCacheName, cache.Name, "null name resets to default");
                cache.Telemetry = null;
                AssertTrue(cache.Telemetry != null && cache.Telemetry.Enable, "null settings reset to defaults");
                AssertFalse(cache.Telemetry.TraceLookups, "TraceLookups defaults to false");
                AssertFalse(cache.Telemetry.RecordExceptionMessages, "RecordExceptionMessages defaults to false");
            }

            return Task.CompletedTask;
        }

        private static async Task NoListenerAsync(CancellationToken token)
        {
            // Other cases may attach listeners concurrently; this case proves that every code path, including failures,
            // works regardless, and that a cache with telemetry fully disabled behaves identically.
            FaultablePersistence<string, string> persistence = new FaultablePersistence<string, string>();

            foreach (CacheBase<string, string> cache in BothPolicies(UniqueName(), 3, 1, persistence))
            {
                using (cache)
                {
                    cache.ExpirationIntervalMs = 10;
                    cache.AddReplace("a", "1");
                    cache.AddReplace("b", "2", TimeSpan.FromMilliseconds(20));
                    cache.Get("a");
                    AssertThrows<KeyNotFoundException>(() => cache.Get("missing"), "Get miss still throws");
                    cache.GetOrAdd("c", k => "3");
                    cache.AddOrUpdate("c", "x", (k, v) => v + "!");
                    await cache.GetOrAddAsync("d", k => Task.FromResult("4"), (DateTime?)null, token).ConfigureAwait(false);
                    cache.AddReplace("e", "5");
                    cache.TryRemove("e", out _);
                    cache.Remove("missing");

                    persistence.FailWrite = true;
                    AssertThrows<IOException>(() => cache.AddReplace("f", "6"), "persistence failure still propagates");
                    persistence.FailWrite = false;

                    AssertThrows<InvalidOperationException>(() => cache.GetOrAdd("g", k => throw new InvalidOperationException("boom")), "factory failure still propagates");

                    await cache.PrepopulateAsync(token).ConfigureAwait(false);
                    cache.Clear();
                    AssertEqual(0, cache.Count(), "clear works");
                }
            }
        }

        private static Task DisabledAsync(CancellationToken token)
        {
            string name = UniqueName();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                foreach (CacheBase<string, string> cache in BothPolicies(name))
                {
                    using (cache)
                    {
                        cache.Telemetry.Enable = false;
                        cache.AddReplace("a", "1");
                        cache.Get("a");
                        cache.TryGet("missing", out _);
                        capture.CollectGauges();
                    }
                }

                AssertEqual(0, capture.Measurements.Count, "no measurements when disabled");
                AssertEqual(0, capture.Activities.Count, "no spans when disabled");
            }

            return Task.CompletedTask;
        }

        private static async Task OperationMetricsAsync(CancellationToken token)
        {
            string name = UniqueName();

            using (TelemetryCapture capture = new TelemetryCapture(name, true, false))
            {
                foreach (CacheBase<string, string> cache in BothPolicies(name))
                {
                    using (cache)
                    {
                        cache.AddReplace("a", "1");
                        cache.Get("a");
                        AssertThrows<KeyNotFoundException>(() => cache.Get("missing"), "Get miss");
                        cache.GetOrDefault("a");
                        cache.GetOrDefault("missing");
                        cache.TryGet("a", out _);
                        cache.TryGet("missing", out _);
                        cache.Contains("a");
                        cache.Contains("missing");
                        cache.GetOrAdd("b", k => "2");
                        await cache.GetOrAddAsync("c", k => Task.FromResult("3"), (DateTime?)null, token).ConfigureAwait(false);
                        cache.AddOrUpdate("b", "x", (k, v) => v + "!");
                        await cache.AddOrUpdateAsync("b", "x", (k, v) => Task.FromResult(v + "?"), null, token).ConfigureAwait(false);
                        cache.Remove("a");
                        cache.Remove("missing");
                        await cache.RemoveAsync("c", token).ConfigureAwait(false);
                        cache.TryRemove("b", out _);
                        cache.TryRemove("missing", out _);
                        await cache.ClearAsync(token).ConfigureAwait(false);
                        AssertThrows<ArgumentNullException>(() => cache.AddReplace(null, "x"), "null key");
                    }
                }

                foreach (string type in new[] { N.CacheTypeFifo, N.CacheTypeLru })
                {
                    string[][] expected = new string[][]
                    {
                        new[] { N.OperationGet, N.OutcomeSuccess },
                        new[] { N.OperationGet, N.OutcomeMiss },
                        new[] { N.OperationGetOrDefault, N.OutcomeSuccess },
                        new[] { N.OperationGetOrDefault, N.OutcomeMiss },
                        new[] { N.OperationTryGet, N.OutcomeSuccess },
                        new[] { N.OperationTryGet, N.OutcomeMiss },
                        new[] { N.OperationContains, N.OutcomeSuccess },
                        new[] { N.OperationContains, N.OutcomeMiss },
                        new[] { N.OperationGetOrAdd, N.OutcomeSuccess },
                        new[] { N.OperationAddOrUpdate, N.OutcomeSuccess },
                        new[] { N.OperationAddReplace, N.OutcomeSuccess },
                        new[] { N.OperationAddReplace, N.OutcomeError },
                        new[] { N.OperationRemove, N.OutcomeSuccess },
                        new[] { N.OperationRemove, N.OutcomeMiss },
                        new[] { N.OperationTryRemove, N.OutcomeSuccess },
                        new[] { N.OperationTryRemove, N.OutcomeMiss },
                        new[] { N.OperationClear, N.OutcomeSuccess }
                    };

                    foreach (string[] pair in expected)
                    {
                        List<CapturedMeasurement> found = capture.For(N.OperationDuration,
                            N.AttributeCacheType, type, N.AttributeOperation, pair[0], N.AttributeOutcome, pair[1]);
                        AssertTrue(found.Count > 0, type + " " + pair[0] + " " + pair[1] + " recorded");
                        AssertTrue(found.All(m => m.Unit == "s" && m.Value >= 0), "duration is in non-negative seconds");
                    }

                    AssertTrue(capture.For(N.OperationDuration, N.AttributeCacheType, type, N.AttributeOutcome, N.OutcomeError,
                        N.AttributeErrorType, "ArgumentNullException").Count == 1, type + " error.type recorded");
                    AssertEqual(1.0, capture.Sum(N.Errors, N.AttributeCacheType, type, N.AttributeComponent, N.ComponentOperation,
                        N.AttributeErrorType, "ArgumentNullException"), type + " operation errors counter");
                    AssertEqual(0, capture.For(N.OperationDuration, N.AttributeOutcome, N.OutcomeMiss, N.AttributeErrorType, "KeyNotFoundException").Count,
                        "a miss is not tagged as an error");
                }
            }
        }

        private static Task LookupCountersAsync(CancellationToken token)
        {
            string name = UniqueName();

            using (TelemetryCapture capture = new TelemetryCapture(name, true, false))
            {
                foreach (CacheBase<string, string> cache in BothPolicies(name))
                {
                    using (cache)
                    {
                        cache.AddReplace("a", "1");
                        cache.Get("a");
                        cache.TryGet("a", out _);
                        cache.GetOrDefault("missing");
                        cache.GetOrAdd("a", k => "x");
                        cache.GetOrAdd("b", k => "2");

                        CacheStatistics stats = cache.GetStatistics();
                        string type = cache is FIFOCache<string, string> ? N.CacheTypeFifo : N.CacheTypeLru;
                        AssertEqual((double)stats.HitCount, capture.Sum(N.Lookups, N.AttributeCacheType, type, N.AttributeLookupResult, N.LookupHit), type + " hits match statistics");
                        AssertEqual((double)stats.MissCount, capture.Sum(N.Lookups, N.AttributeCacheType, type, N.AttributeLookupResult, N.LookupMiss), type + " misses match statistics");
                        AssertEqual(3.0, capture.Sum(N.Lookups, N.AttributeCacheType, type, N.AttributeLookupResult, N.LookupHit), type + " three hits");
                        AssertEqual(2.0, capture.Sum(N.Lookups, N.AttributeCacheType, type, N.AttributeLookupResult, N.LookupMiss), type + " two misses");
                    }
                }
            }

            return Task.CompletedTask;
        }

        private static Task EvictionCountersAsync(CancellationToken token)
        {
            string name = UniqueName();

            using (TelemetryCapture capture = new TelemetryCapture(name, true, false))
            {
                foreach (CacheBase<string, string> cache in BothPolicies(name, 3, 2))
                {
                    using (cache)
                    {
                        for (int i = 0; i < 4; i++) cache.AddReplace("k" + i, "v");
                    }
                }

                foreach (CacheBase<string, string> cache in BothPolicies(name, 100, 1))
                {
                    using (cache)
                    {
                        cache.MaxMemoryBytes = 10;
                        cache.AddReplace("a", "aaaa");
                        cache.AddReplace("b", "bbbb");
                    }
                }

                AssertEqual(2.0, capture.Sum(N.Evictions, N.AttributeCacheType, N.CacheTypeFifo, N.AttributeEvictionReason, N.EvictionReasonCapacity), "fifo capacity evictions");
                AssertEqual(2.0, capture.Sum(N.Evictions, N.AttributeCacheType, N.CacheTypeLru, N.AttributeEvictionReason, N.EvictionReasonCapacity), "lru capacity evictions");
                AssertEqual(1.0, capture.Sum(N.Evictions, N.AttributeCacheType, N.CacheTypeFifo, N.AttributeEvictionReason, N.EvictionReasonMemory), "fifo memory evictions");
                AssertEqual(1.0, capture.Sum(N.Evictions, N.AttributeCacheType, N.CacheTypeLru, N.AttributeEvictionReason, N.EvictionReasonMemory), "lru memory evictions");
            }

            return Task.CompletedTask;
        }

        private static Task OperationSpansAsync(CancellationToken token)
        {
            string name = UniqueName();

            using (TelemetryCapture capture = new TelemetryCapture(name, false, true))
            {
                using (FIFOCache<string, string> cache = new FIFOCache<string, string>(10, 1))
                {
                    cache.Name = name;
                    cache.AddReplace("a", "1");
                    cache.Get("a");
                    cache.TryGet("a", out _);
                    cache.Contains("a");

                    AssertEqual(0, capture.Spans("caching get").Count, "no lookup spans by default");
                    AssertEqual(0, capture.Spans("caching try_get").Count, "no try_get spans by default");

                    cache.Telemetry.TraceLookups = true;
                    cache.Get("a");
                    AssertThrows<KeyNotFoundException>(() => cache.Get("missing"), "miss");
                    cache.Remove("a");
                    cache.Clear();
                }

                Activity add = capture.Spans("caching add_replace").Single();
                AssertEqual(ActivityStatusCode.Ok, add.Status, "add_replace status ok");
                AssertEqual(ActivityKind.Internal, add.Kind, "internal span");
                AssertEqual(N.CacheTypeFifo, add.GetTagItem(N.AttributeCacheType) as string, "cache.type tag");
                AssertEqual(N.OperationAddReplace, add.GetTagItem(N.AttributeOperation) as string, "cache.operation tag");

                List<Activity> gets = capture.Spans("caching get");
                AssertEqual(2, gets.Count, "lookup spans when TraceLookups is set");
                AssertTrue(gets.Any(a => Equals(a.GetTagItem(N.AttributeHit), true) && a.Status == ActivityStatusCode.Ok), "hit span");
                AssertTrue(gets.Any(a => Equals(a.GetTagItem(N.AttributeHit), false) && (a.GetTagItem(N.AttributeOutcome) as string) == N.OutcomeMiss
                    && a.Status == ActivityStatusCode.Ok), "miss span is not an error");
                AssertEqual(1, capture.Spans("caching remove").Count, "remove span");
                AssertEqual(1, capture.Spans("caching clear").Count, "clear span");
                AssertTrue(capture.Activities.All(a => a.GetTagItem("key") == null), "keys are never recorded");
            }

            return Task.CompletedTask;
        }

        private static async Task WorkflowStagesAsync(CancellationToken token)
        {
            string name = UniqueName();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                using (LRUCache<string, string> cache = new LRUCache<string, string>(10, 1))
                {
                    cache.Name = name;
                    cache.GetOrAdd("a", k => "1");
                    await cache.GetOrAddAsync("b", k => Task.FromResult("2"), (DateTime?)null, token).ConfigureAwait(false);
                    cache.AddOrUpdate("a", "x", (k, v) => v + "!");
                    cache.GetOrAdd("a", k => "never");
                }

                List<Activity> getOrAdds = capture.Spans("caching get_or_add");
                AssertEqual(3, getOrAdds.Count, "get_or_add spans");

                Activity first = getOrAdds.First(a => Equals(a.GetTagItem(N.AttributeHit), false));
                List<Activity> children = capture.Activities.Where(a => a.ParentSpanId == first.SpanId).ToList();
                AssertTrue(children.Any(a => a.DisplayName == N.SpanLockWait), "lock wait stage under get_or_add");
                AssertTrue(children.Any(a => a.DisplayName == N.SpanValueFactory), "value factory stage under get_or_add");
                AssertTrue(children.Any(a => a.DisplayName == "caching add_replace"), "add_replace under get_or_add");

                Activity hit = getOrAdds.First(a => Equals(a.GetTagItem(N.AttributeHit), true));
                AssertFalse(capture.Activities.Any(a => a.ParentSpanId == hit.SpanId && a.DisplayName == N.SpanValueFactory), "no factory stage on hit");

                Activity update = capture.Spans("caching add_or_update").Single();
                AssertTrue(capture.Activities.Any(a => a.ParentSpanId == update.SpanId && a.DisplayName == N.SpanValueFactory), "update factory stage");

                AssertTrue(capture.For(N.LockWaitDuration, N.AttributeOperation, N.OperationGetOrAdd).Count >= 3, "lock wait histogram for get_or_add");
                AssertTrue(capture.For(N.LockWaitDuration, N.AttributeOperation, N.OperationAddOrUpdate).Count >= 1, "lock wait histogram for add_or_update");
            }
        }

        private static async Task ValueFactoryFailureAsync(CancellationToken token)
        {
            string name = UniqueName();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                using (FIFOCache<string, string> cache = new FIFOCache<string, string>(10, 1))
                {
                    cache.Name = name;
                    AssertThrows<InvalidOperationException>(() => cache.GetOrAdd("a", k => throw new InvalidOperationException("secret-payload")), "sync factory throws");

                    cache.Telemetry.RecordExceptionMessages = true;
                    try
                    {
                        await cache.GetOrAddAsync("b", k => Task.FromException<string>(new TimeoutException("visible-message")), (DateTime?)null, token).ConfigureAwait(false);
                        throw new InvalidOperationException("expected TimeoutException");
                    }
                    catch (TimeoutException)
                    {
                    }
                }

                List<Activity> factories = capture.Spans(N.SpanValueFactory);
                AssertEqual(2, factories.Count, "two factory spans");
                AssertTrue(factories.All(a => a.Status == ActivityStatusCode.Error), "factory spans are errors");

                Activity syncFactory = factories.Single(a => (a.GetTagItem(N.AttributeErrorType) as string) == "InvalidOperationException");
                ActivityEvent ev = syncFactory.Events.Single(e => e.Name == "exception");
                AssertEqual("System.InvalidOperationException", ev.Tags.First(t => t.Key == "exception.type").Value as string, "exception.type");
                AssertFalse(ev.Tags.Any(t => t.Key == "exception.message"), "message omitted by default");

                Activity asyncFactory = factories.Single(a => (a.GetTagItem(N.AttributeErrorType) as string) == "TimeoutException");
                AssertTrue(asyncFactory.Events.Single(e => e.Name == "exception").Tags.Any(t => t.Key == "exception.message" && (t.Value as string) == "visible-message"),
                    "message recorded when RecordExceptionMessages is set");

                AssertEqual(2, capture.Spans("caching get_or_add").Count(a => a.Status == ActivityStatusCode.Error), "operation spans are errors");
                AssertEqual(1.0, capture.Sum(N.Errors, N.AttributeComponent, N.ComponentOperation, N.AttributeErrorType, "InvalidOperationException"), "errors counter");
                AssertEqual(1, capture.For(N.OperationDuration, N.AttributeOperation, N.OperationGetOrAdd, N.AttributeOutcome, N.OutcomeError,
                    N.AttributeErrorType, "TimeoutException").Count, "operation histogram error.type");
            }
        }

        private static async Task CancellationAsync(CancellationToken token)
        {
            string name = UniqueName();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                using (FIFOCache<string, string> cache = new FIFOCache<string, string>(10, 1))
                {
                    cache.Name = name;
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        cts.Cancel();
                        try
                        {
                            await cache.GetOrAddAsync("a", k => Task.FromResult("1"), (DateTime?)null, cts.Token).ConfigureAwait(false);
                            throw new InvalidOperationException("expected cancellation");
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }

                AssertEqual(1, capture.For(N.OperationDuration, N.AttributeOperation, N.OperationGetOrAdd, N.AttributeOutcome, N.OutcomeCanceled).Count, "canceled outcome");
                AssertEqual(0.0, capture.Sum(N.Errors), "cancellation is not an error");
                AssertEqual(N.OutcomeCanceled, capture.Spans("caching get_or_add").Single().GetTagItem(N.AttributeOutcome) as string, "span outcome canceled");
            }
        }

        private static async Task PersistenceAsync(CancellationToken token)
        {
            string name = UniqueName();
            FaultablePersistence<string, string> persistence = new FaultablePersistence<string, string>();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                using (FIFOCache<string, string> cache = new FIFOCache<string, string>(2, 1, persistence))
                {
                    cache.Name = name;
                    cache.AddReplace("a", "1");
                    cache.AddReplace("b", "2");
                    cache.AddReplace("c", "3");
                    await cache.RemoveAsync("c", token).ConfigureAwait(false);
                    cache.TryRemove("b", out _);
                    await cache.ClearAsync(token).ConfigureAwait(false);
                }

                AssertEqual(3.0, capture.Sum(N.PersistenceCalls, N.AttributePersistenceOperation, N.PersistenceWrite, N.AttributeOutcome, N.OutcomeSuccess), "write calls");
                AssertEqual(3.0, capture.Sum(N.PersistenceCalls, N.AttributePersistenceOperation, N.PersistenceDelete, N.AttributeOutcome, N.OutcomeSuccess), "delete calls (evict, remove, try_remove)");
                AssertEqual(1.0, capture.Sum(N.PersistenceCalls, N.AttributePersistenceOperation, N.PersistenceClear), "clear calls");
                AssertEqual(7, capture.For(N.PersistenceDuration).Count, "persistence duration per call");

                List<Activity> writes = capture.Spans("persistence write");
                AssertEqual(3, writes.Count, "write spans");
                AssertTrue(writes.All(a => a.Kind == ActivityKind.Client && a.Status == ActivityStatusCode.Ok), "client spans with ok status");
                AssertEqual("FaultablePersistence`2", writes[0].GetTagItem(N.AttributePersistenceDriver) as string, "driver type on span");
                AssertEqual(3, capture.Spans("persistence delete").Count, "delete spans");
                AssertEqual(1, capture.Spans("persistence clear").Count, "clear span");
            }
        }

        private static Task PersistenceFailureAsync(CancellationToken token)
        {
            string name = UniqueName();
            FaultablePersistence<string, string> persistence = new FaultablePersistence<string, string>();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                using (LRUCache<string, string> cache = new LRUCache<string, string>(10, 1, persistence))
                {
                    cache.Name = name;
                    persistence.FailWrite = true;
                    AssertThrows<IOException>(() => cache.AddReplace("a", "1"), "write failure propagates");
                }

                AssertTrue(capture.Sum(N.PersistenceCalls, N.AttributePersistenceOperation, N.PersistenceWrite, N.AttributeOutcome, N.OutcomeError,
                    N.AttributeErrorType, "IOException") >= 1, "persistence error call");
                AssertTrue(capture.Sum(N.Errors, N.AttributeComponent, N.ComponentPersistence, N.AttributeErrorType, "IOException") >= 1, "persistence errors counter");
                AssertTrue(capture.Sum(N.Errors, N.AttributeComponent, N.ComponentOperation, N.AttributeErrorType, "IOException") >= 1, "operation errors counter");

                Activity write = capture.Spans("persistence write").First();
                AssertEqual(ActivityStatusCode.Error, write.Status, "persistence span error");
                AssertFalse(write.Events.SelectMany(e => e.Tags).Any(t => (t.Value as string ?? "").Contains("secret-key-123")), "driver exception message not recorded");
                AssertEqual(ActivityStatusCode.Error, capture.Spans("caching add_replace").First().Status, "operation span error");
            }

            return Task.CompletedTask;
        }

        private static Task TryFailuresRecordedAsync(CancellationToken token)
        {
            string name = UniqueName();
            FaultablePersistence<string, string> persistence = new FaultablePersistence<string, string>();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                using (FIFOCache<string, string> cache = new FIFOCache<string, string>(10, 1, persistence))
                {
                    cache.Name = name;
                    persistence.FailWrite = true;
                    AssertFalse(cache.TryAddReplace("a", "1"), "TryAddReplace returns false");
                    persistence.FailWrite = false;

                    persistence.FailDelete = true;
                    AssertFalse(cache.TryRemove("a", out _), "TryRemove returns false");
                    AssertFalse(cache.TryGet(null, out _), "TryGet returns false");
                }

                AssertEqual(1, capture.For(N.OperationDuration, N.AttributeOperation, N.OperationAddReplace, N.AttributeOutcome, N.OutcomeError, N.AttributeErrorType, "IOException").Count, "add_replace error recorded");
                AssertEqual(1, capture.For(N.OperationDuration, N.AttributeOperation, N.OperationTryRemove, N.AttributeOutcome, N.OutcomeError, N.AttributeErrorType, "IOException").Count, "try_remove error recorded");
                AssertEqual(1, capture.For(N.OperationDuration, N.AttributeOperation, N.OperationTryGet, N.AttributeOutcome, N.OutcomeError, N.AttributeErrorType, "ArgumentNullException").Count, "try_get error recorded");
                AssertEqual(2.0, capture.Sum(N.Errors, N.AttributeComponent, N.ComponentPersistence), "persistence errors recorded");
                AssertEqual(ActivityStatusCode.Error, capture.Spans("caching try_remove").Single().Status, "try_remove span is an error");
            }

            return Task.CompletedTask;
        }

        private static async Task PrepopulateAsync(CancellationToken token)
        {
            string name = UniqueName();
            FaultablePersistence<string, string> persistence = new FaultablePersistence<string, string>();
            persistence.Seed("a", "1");
            persistence.Seed("b", "2");
            persistence.Seed("c", "3");

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                using (FIFOCache<string, string> cache = new FIFOCache<string, string>(10, 1, persistence))
                {
                    cache.Name = name;
                    await cache.PrepopulateAsync(token).ConfigureAwait(false);
                }

                AssertEqual(3.0, capture.Sum(N.Prepopulated), "prepopulated counter");
                AssertEqual(1.0, capture.Sum(N.PersistenceCalls, N.AttributePersistenceOperation, N.PersistenceEnumerate), "enumerate call");
                AssertEqual(3.0, capture.Sum(N.PersistenceCalls, N.AttributePersistenceOperation, N.PersistenceGet), "get calls");
                Activity span = capture.Spans("caching prepopulate").Single();
                AssertEqual(3, Convert.ToInt32(span.GetTagItem(N.AttributeEntryCount)), "entry count on span");
                AssertEqual(4, capture.Activities.Count(a => a.ParentSpanId == span.SpanId), "persistence spans under prepopulate");
                AssertEqual(1, capture.For(N.OperationDuration, N.AttributeOperation, N.OperationPrepopulate, N.AttributeOutcome, N.OutcomeSuccess).Count, "prepopulate duration");
            }
        }

        private static Task ContextPropagationAsync(CancellationToken token)
        {
            string name = UniqueName();
            FaultablePersistence<string, string> persistence = new FaultablePersistence<string, string>();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                using (FIFOCache<string, string> cache = new FIFOCache<string, string>(10, 1, persistence))
                {
                    cache.Name = name;

                    using (Activity parent = _TestSource.StartActivity("host request"))
                    {
                        AssertTrue(parent != null, "test parent span created");
                        cache.AddReplace("a", "1");

                        Activity add = capture.Spans("caching add_replace").Single();
                        AssertEqual(parent.TraceId, add.TraceId, "same trace as the caller");
                        AssertEqual(parent.SpanId, add.ParentSpanId, "cache span is a child of the caller span");

                        Activity write = capture.Spans("persistence write").Single();
                        AssertEqual(add.SpanId, write.ParentSpanId, "persistence span is a child of the cache span");
                    }
                }
            }

            return Task.CompletedTask;
        }

        private static async Task ExpirationSweepAsync(CancellationToken token)
        {
            string name = UniqueName();
            FaultablePersistence<string, string> persistence = new FaultablePersistence<string, string>();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                ActivityTraceId creatorTrace;
                LRUCache<string, string> cache;

                using (Activity creator = _TestSource.StartActivity("host startup"))
                {
                    creatorTrace = creator.TraceId;
                    cache = new LRUCache<string, string>(10, 1, persistence);
                }

                using (cache)
                {
                    cache.Name = name;
                    cache.ExpirationIntervalMs = 20;
                    cache.AddReplace("a", "1", TimeSpan.FromMilliseconds(30));
                    cache.AddReplace("b", "2", TimeSpan.FromMilliseconds(30));

                    await WaitUntilAsync(() => capture.Spans(N.SpanExpirationSweep).Count > 0 && capture.Sum(N.Expirations) >= 2, token).ConfigureAwait(false);

                    capture.CollectGauges();
                    List<CapturedMeasurement> lastSuccess = capture.For(N.ExpirationSweepLastSuccess);
                    AssertTrue(lastSuccess.Count == 1, "last success gauge reported");
                    double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                    AssertTrue(Math.Abs(now - lastSuccess[0].Value) < 30, "last success is a recent unix time");
                }

                AssertEqual(2.0, capture.Sum(N.Expirations), "expirations counter");
                AssertTrue(capture.For(N.ExpirationSweepDuration, N.AttributeOutcome, N.OutcomeSuccess).Count > 0, "sweep duration recorded");

                Activity sweep = capture.Spans(N.SpanExpirationSweep).First();
                AssertEqual(default(ActivitySpanId), sweep.ParentSpanId, "sweep span is a trace root");
                AssertTrue(sweep.TraceId != creatorTrace, "sweep is not part of the creating trace");
                AssertTrue(sweep.Links.Any(l => l.Context.TraceId == creatorTrace), "sweep span links to the creating trace");
                AssertEqual(2, Convert.ToInt32(sweep.GetTagItem(N.AttributeEntryCount)), "entry count on sweep span");
                AssertTrue(capture.Activities.Count(a => a.ParentSpanId == sweep.SpanId && a.DisplayName == "persistence delete") == 2, "persistence deletes under sweep");
            }
        }

        private static async Task ExpirationFailureAsync(CancellationToken token)
        {
            string name = UniqueName();
            FaultablePersistence<string, string> persistence = new FaultablePersistence<string, string>();

            using (TelemetryCapture capture = new TelemetryCapture(name))
            {
                using (FIFOCache<string, string> cache = new FIFOCache<string, string>(10, 1, persistence))
                {
                    cache.Name = name;
                    cache.ExpirationIntervalMs = 20;
                    cache.AddReplace("a", "1", TimeSpan.FromMilliseconds(30));
                    persistence.FailDelete = true;

                    await WaitUntilAsync(() => capture.Sum(N.Errors, N.AttributeComponent, N.ComponentExpiration) >= 1, token).ConfigureAwait(false);
                }

                AssertEqual(1.0, capture.Sum(N.Errors, N.AttributeComponent, N.ComponentExpiration, N.AttributeErrorType, "IOException"), "expiration error counter");
                AssertEqual(1, capture.For(N.ExpirationSweepDuration, N.AttributeOutcome, N.OutcomeError, N.AttributeErrorType, "IOException").Count, "sweep error duration");
                AssertTrue(capture.Spans(N.SpanExpirationSweep).Any(a => a.Status == ActivityStatusCode.Error), "sweep span error");
            }
        }

        private static Task GaugesAsync(CancellationToken token)
        {
            string name = UniqueName();

            using (TelemetryCapture capture = new TelemetryCapture(name, true, false))
            {
                FIFOCache<string, string> cache = new FIFOCache<string, string>(50, 5);
                cache.Name = name;
                cache.MaxMemoryBytes = 1000;
                cache.ExpirationIntervalMs = 250;
                cache.AddReplace("a", "12345");
                cache.AddReplace("b", "12345");

                capture.CollectGauges();
                AssertEqual(2.0, capture.Sum(N.Entries), "entries gauge");
                AssertEqual(50.0, capture.Sum(N.Capacity), "capacity gauge");
                AssertEqual(5.0, capture.Sum(N.EvictCount), "evict count gauge");
                AssertEqual(20.0, capture.Sum(N.MemoryUsage), "memory usage gauge");
                AssertEqual(1000.0, capture.Sum(N.MemoryLimit), "memory limit gauge");
                AssertEqual(0.25, capture.Sum(N.ExpirationInterval), "expiration interval gauge in seconds");
                AssertEqual(0.0, capture.Sum(N.LockWaiting), "no lock waiters");
                List<CapturedMeasurement> build = capture.For(N.BuildInfo, N.AttributeCacheType, N.CacheTypeFifo);
                AssertEqual(1, build.Count, "build info gauge");
                AssertTrue(!String.IsNullOrEmpty(build[0].Tags[N.AttributeVersion]) && build[0].Tags[N.AttributeVersion] != "unknown", "build info carries version");
                AssertEqual("By", capture.For(N.MemoryUsage)[0].Unit, "memory unit is bytes");

                cache.Dispose();
                int before = capture.Measurements.Count;
                capture.CollectGauges();
                AssertEqual(before, capture.Measurements.Count, "disposed caches are not reported");
            }

            return Task.CompletedTask;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(25, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Condition was not met within the timeout.");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition) throw new InvalidOperationException("Assertion failed: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Assertion failed: " + message + ". Expected <" + expected + "> but got <" + actual + ">.");
        }

        private static void AssertThrows<TException>(Action action, string message) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException("Assertion failed: " + message + ". Expected " + typeof(TException).Name + ".");
        }
    }
}
