# Telemetry

Starting with v5.1.0, the Caching library emits metrics and traces through the .NET BCL `System.Diagnostics.Metrics.Meter` and `System.Diagnostics.ActivitySource` APIs. The library takes no dependency on OpenTelemetry, Radiant, or any exporter, and never opens a connection to a backend. It emits; your host collects and exports to Prometheus, Tempo, Grafana, or any OTLP backend.

When nothing is subscribed, the cost is a few boolean checks per operation and no allocation: timestamps are only taken when a listener has enabled the instrument, and spans are only created when an `ActivityListener` samples the source.

## Contents

1. [Sources](#sources)
2. [Subscribing from a host](#subscribing-from-a-host)
3. [Configuration](#configuration)
4. [Labels](#labels)
5. [Metrics catalog](#metrics-catalog)
6. [Spans catalog](#spans-catalog)
7. [Recommended PromQL and alerts](#recommended-promql-and-alerts)
8. [Suggested Grafana dashboard](#suggested-grafana-dashboard)
9. [Guarantees and limitations](#guarantees-and-limitations)

## Sources

| Kind | Name | Constant |
| --- | --- | --- |
| Meter | `Caching` | `CacheTelemetryNames.MeterName` |
| ActivitySource | `Caching` | `CacheTelemetryNames.ActivitySourceName` |

Both carry the library version (for example `5.1.0`). Every metric name, attribute key, span name, and attribute value used by the library is a constant on `Caching.CacheTelemetryNames`. These names are public contract: dashboards and alerts depend on them and they will not change without a major version.

## Subscribing from a host

### Radiant

```csharp
RadiantSettings settings = new RadiantSettings("orders-api");
settings.Sources.AddMeter("Caching");             // or CacheTelemetryNames.MeterName
settings.Sources.AddActivitySource("Caching");    // or CacheTelemetryNames.ActivitySourceName

using (RadiantHost host = RadiantHost.Start(settings))
{
    // run the application
}
```

### OpenTelemetry .NET SDK

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddMeter(CacheTelemetryNames.MeterName)
        .AddPrometheusExporter())
    .WithTracing(t => t
        .AddSource(CacheTelemetryNames.ActivitySourceName)
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://127.0.0.1:4317")));
```

### Raw BCL listeners (tests, custom collectors)

```csharp
MeterListener listener = new MeterListener();
listener.InstrumentPublished = (instrument, l) =>
{
    if (instrument.Meter.Name == CacheTelemetryNames.MeterName) l.EnableMeasurementEvents(instrument);
};
listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => { /* ... */ });
listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => { /* ... */ });
listener.Start();
```

See `src/Test.Shared/TelemetryCapture.cs` for a complete in-memory listener for both signals.

Runtime metrics (GC, thread pool, allocations) are the host's responsibility. Radiant enables them by default; with the OpenTelemetry SDK add `.AddRuntimeInstrumentation()`.

## Configuration

Telemetry is on by default. Settings are per cache instance and may be changed at any time.

```csharp
LRUCache<string, Product> cache = new LRUCache<string, Product>(10000, 100);
cache.Name = "products";                          // cache.name label; keep it short and static
cache.Telemetry.Enable = true;                    // master switch (default true)
cache.Telemetry.EnableMetrics = true;             // default true
cache.Telemetry.EnableTraces = true;              // default true
cache.Telemetry.TraceLookups = false;             // spans for Get/TryGet/GetOrDefault/Contains (default false)
cache.Telemetry.RecordExceptionMessages = false;  // add exception.message to span events (default false)
```

| Setting | Default | Effect |
| --- | --- | --- |
| `CacheBase.Name` | `default` | Value of the `cache.name` label on every metric and span. Use a small static set of names; never per-request or per-user values. Null or empty resets to `default`. |
| `Telemetry.Enable` | `true` | When false, the instance emits nothing and is excluded from gauges. |
| `Telemetry.EnableMetrics` | `true` | Metrics for this instance. |
| `Telemetry.EnableTraces` | `true` | Spans for this instance. |
| `Telemetry.TraceLookups` | `false` | Pure in-memory lookups take nanoseconds; a span on each one is expensive and noisy. Lookups are always measured by metrics. Turn this on only while investigating. |
| `Telemetry.RecordExceptionMessages` | `false` | Exception messages from persistence drivers and value factories can contain keys, paths, or other data. By default only `exception.type` and `exception.stacktrace` are recorded. |

Give every cache in a process a distinct `Name`. Two caches with the same name and type report under the same series, so gauge values collide.

## Labels

All metric labels are bounded. Keys, values, and free-form text never appear on metrics or spans.

| Attribute | Prometheus label | Values |
| --- | --- | --- |
| `cache.name` | `cache_name` | `CacheBase.Name` (application-defined, static) |
| `cache.type` | `cache_type` | `fifo`, `lru` |
| `cache.operation` | `cache_operation` | `get`, `get_or_default`, `try_get`, `contains`, `add_replace`, `get_or_add`, `add_or_update`, `remove`, `try_remove`, `clear`, `prepopulate` |
| `outcome` | `outcome` | `success`, `miss`, `error`, `canceled` |
| `cache.lookup.result` | `cache_lookup_result` | `hit`, `miss` |
| `cache.eviction.reason` | `cache_eviction_reason` | `capacity`, `memory` |
| `cache.component` | `cache_component` | `operation`, `persistence`, `expiration` |
| `cache.persistence.operation` | `cache_persistence_operation` | `write`, `delete`, `clear`, `get`, `enumerate` |
| `error.type` | `error_type` | Exception type name, for example `IOException` (present only when `outcome` is `error`) |
| `cache.version` | `cache_version` | Library version (build info only) |

`outcome="miss"` means the operation completed but the key was absent (Get, GetOrDefault, TryGet, Contains, Remove, TryRemove). `Get` throws `KeyNotFoundException` on a miss; that is recorded as a miss, not as an error.

## Metrics catalog

Prometheus names assume the standard OpenTelemetry Prometheus exporter (dots to underscores, unit suffix, `_total` on counters).

| Instrument | Prometheus name | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- | --- |
| `caching.operation.duration` | `caching_operation_duration_seconds` | Histogram | s | name, type, operation, outcome, error.type | Duration of every public operation. `_count` is the operation rate. |
| `caching.lookups` | `caching_lookups_total` | Counter | {lookup} | name, type, lookup.result | Hits and misses from Get, GetOrDefault, TryGet, GetOrAdd. Matches `GetStatistics()`. |
| `caching.evictions` | `caching_evictions_total` | Counter | {entry} | name, type, eviction.reason | Entries evicted for capacity or memory. |
| `caching.expirations` | `caching_expirations_total` | Counter | {entry} | name, type | Entries removed by the expiration task. |
| `caching.prepopulated` | `caching_prepopulated_total` | Counter | {entry} | name, type | Entries loaded by Prepopulate. |
| `caching.errors` | `caching_errors_total` | Counter | {error} | name, type, component, error.type | Failures. Cancellations are not counted. |
| `caching.persistence.duration` | `caching_persistence_duration_seconds` | Histogram | s | name, type, persistence.operation, outcome, error.type | Latency of each call into `IPersistenceDriver`. |
| `caching.persistence.calls` | `caching_persistence_calls_total` | Counter | {call} | name, type, persistence.operation, outcome, error.type | Calls into the persistence driver. |
| `caching.expiration.sweep.duration` | `caching_expiration_sweep_duration_seconds` | Histogram | s | name, type, outcome, error.type | Duration of each background expiration sweep, including persistence deletes and Expired handlers. |
| `caching.expiration.sweep.last_success` | `caching_expiration_sweep_last_success_seconds` | Gauge | s | name, type | Unix time of the last successful sweep. Absent until the first sweep completes. |
| `caching.lock.wait.duration` | `caching_lock_wait_duration_seconds` | Histogram | s | name, type, operation | Wait for the single-slot atomic lock that serializes GetOrAdd and AddOrUpdate. |
| `caching.lock.waiting` | `caching_lock_waiting` | Gauge | {operation} | name, type | Operations currently queued on the atomic lock. |
| `caching.entries` | `caching_entries` | Gauge | {entry} | name, type | Current entry count. |
| `caching.capacity` | `caching_capacity` | Gauge | {entry} | name, type | Configured capacity. |
| `caching.evict_count` | `caching_evict_count` | Gauge | {entry} | name, type | Configured eviction batch size. |
| `caching.memory.usage` | `caching_memory_usage_bytes` | Gauge | By | name, type | Estimated memory in use (0 unless `MaxMemoryBytes` is set). |
| `caching.memory.limit` | `caching_memory_limit_bytes` | Gauge | By | name, type | Configured `MaxMemoryBytes` (0 = unlimited). |
| `caching.expiration.interval` | `caching_expiration_interval_seconds` | Gauge | s | name, type | Configured `ExpirationIntervalMs`, in seconds. |
| `caching.build.info` | `caching_build_info` | Gauge | {cache} | name, type, version | 1 per live cache instance. |

Histograms advise explicit bucket boundaries from 5 microseconds to 10 seconds (`InstrumentAdvice`), so sub-millisecond cache operations resolve correctly in collectors that honor advice (OpenTelemetry .NET 1.10+). No quantiles are computed in process; derive p50/p95/p99 in Grafana with `histogram_quantile`.

Gauges are observed from a weak registry of live caches. Disposed caches stop reporting immediately; undisposed caches stop when garbage collected.

## Spans catalog

| Span name | Kind | Parent | When | Attributes |
| --- | --- | --- | --- | --- |
| `caching add_replace` | Internal | caller's current span | AddReplace / AddReplaceAsync / TryAddReplace | `cache.name`, `cache.type`, `cache.operation`, `outcome`, `error.type` |
| `caching get_or_add` | Internal | caller | GetOrAdd / GetOrAddAsync / TryGetOrAdd | as above plus `cache.hit` |
| `caching add_or_update` | Internal | caller | AddOrUpdate / AddOrUpdateAsync | as above |
| `caching remove`, `caching try_remove` | Internal | caller | Remove / RemoveAsync / TryRemove | as above |
| `caching clear` | Internal | caller | Clear / ClearAsync | as above |
| `caching prepopulate` | Internal | caller | Prepopulate / PrepopulateAsync | as above plus `cache.entry_count` |
| `caching get`, `caching get_or_default`, `caching try_get`, `caching contains` | Internal | caller | only when `Telemetry.TraceLookups` is true | as above plus `cache.hit` |
| `stage:lock_wait` | Internal | get_or_add / add_or_update | waiting for the atomic lock | `cache.name`, `cache.type` |
| `stage:value_factory` | Internal | get_or_add / add_or_update | running the caller's value or update factory | `cache.name`, `cache.type` |
| `persistence write`, `persistence delete`, `persistence clear`, `persistence get`, `persistence enumerate` | Client | the cache span that triggered it, or the sweep | every `IPersistenceDriver` call | `cache.name`, `cache.type`, `cache.persistence.operation`, `cache.persistence.driver` (driver type name) |
| `caching expiration_sweep` | Internal | none (trace root), with a link to the trace that constructed the cache | a sweep that removed at least one entry, or failed | `cache.name`, `cache.type`, `cache.entry_count` |

Status is set explicitly on every span: `Ok` on success and on misses, `Error` on failure. Failures add an OpenTelemetry `exception` event with `exception.type` and `exception.stacktrace` (plus `exception.message` only when `RecordExceptionMessages` is true). Canceled operations carry `outcome=canceled`.

Context propagation: cache spans start under `Activity.Current`, so inside a Watson route or any instrumented handler they nest under the request span in one trace. The expiration task is started from the constructor and would otherwise inherit whatever span was current at construction; it explicitly detaches, so each sweep is its own root, linked back to the creating trace. Sweeps that find nothing to expire produce no span (they would otherwise create one trace per cache per second), but are always measured by the sweep histogram.

## Recommended PromQL and alerts

Hit ratio per cache:

```promql
sum by (cache_name) (rate(caching_lookups_total{cache_lookup_result="hit"}[5m]))
/
sum by (cache_name) (rate(caching_lookups_total[5m]))
```

Operation p95 by operation:

```promql
histogram_quantile(0.95, sum by (cache_name, cache_operation, le) (rate(caching_operation_duration_seconds_bucket[5m])))
```

Persistence p95 by driver operation:

```promql
histogram_quantile(0.95, sum by (cache_name, cache_persistence_operation, le) (rate(caching_persistence_duration_seconds_bucket[5m])))
```

Fill ratio:

```promql
caching_entries / caching_capacity
```

Suggested alerts:

```yaml
groups:
  - name: caching
    rules:
      - alert: CachingPersistenceErrors
        expr: sum by (cache_name, cache_persistence_operation, error_type) (rate(caching_persistence_calls_total{outcome="error"}[5m])) > 0
        for: 5m
        annotations:
          summary: "Persistence driver failing for cache {{ $labels.cache_name }} ({{ $labels.cache_persistence_operation }}, {{ $labels.error_type }})"

      - alert: CachingExpirationStalled
        # The sweep should succeed every ExpirationIntervalMs. Ten intervals (minimum 60s) without success means the expiration task stopped.
        expr: time() - caching_expiration_sweep_last_success_seconds > clamp_min(10 * caching_expiration_interval_seconds, 60)
        for: 2m
        annotations:
          summary: "Expiration task for cache {{ $labels.cache_name }} has not completed a sweep recently"

      - alert: CachingExpirationErrors
        expr: sum by (cache_name, error_type) (increase(caching_errors_total{cache_component="expiration"}[10m])) > 0
        annotations:
          summary: "Expiration sweep failed for cache {{ $labels.cache_name }} ({{ $labels.error_type }})"

      - alert: CachingLowHitRatio
        expr: |
          (sum by (cache_name) (rate(caching_lookups_total{cache_lookup_result="hit"}[15m]))
           / sum by (cache_name) (rate(caching_lookups_total[15m]))) < 0.5
          and sum by (cache_name) (rate(caching_lookups_total[15m])) > 1
        for: 15m
        annotations:
          summary: "Hit ratio below 50% for cache {{ $labels.cache_name }}"

      - alert: CachingEvictionChurn
        expr: sum by (cache_name, cache_eviction_reason) (rate(caching_evictions_total[5m])) > 100
        for: 10m
        annotations:
          summary: "Cache {{ $labels.cache_name }} evicting more than 100 entries/s ({{ $labels.cache_eviction_reason }}); capacity or memory limit may be too small"

      - alert: CachingLockContention
        expr: histogram_quantile(0.95, sum by (cache_name, le) (rate(caching_lock_wait_duration_seconds_bucket[5m]))) > 0.1
        for: 10m
        annotations:
          summary: "GetOrAdd/AddOrUpdate on cache {{ $labels.cache_name }} waiting more than 100ms at p95 for the atomic lock"
```

## Suggested Grafana dashboard

This is a library, so it ships no compose stack or dashboard JSON; the host application owns those. A host that uses this library should add a **Cache** dashboard to its product folder with these rows, using the queries above:

| Row | Panels |
| --- | --- |
| Overview | Hit ratio per cache; entries / capacity; memory usage / limit; operations per second by `cache_operation` |
| Latency | Operation p50/p95/p99 by `cache_operation`; lock wait p95; value factory time (from traces, `stage:value_factory`) |
| Churn | Evictions/s by `cache_eviction_reason`; expirations/s; prepopulated entries |
| Persistence | Calls/s by `cache_persistence_operation` and `outcome`; persistence p95 by operation; errors by `error_type` |
| Background | Sweep duration p95; time since last successful sweep; expiration errors |
| Errors | `caching_errors_total` by `cache_component` and `error_type`; operation outcome breakdown |

From a slow request trace in Tempo, the cache spans show whether time went to the atomic lock (`stage:lock_wait`), the caller's own factory (`stage:value_factory`), or the persistence driver (`persistence *`).

## Guarantees and limitations

- Instrumentation is best-effort. Every recording path is guarded, and a failing listener can never change a cache result or exception.
- Exceptions thrown by cache operations, persistence drivers, value factories, and event handlers propagate exactly as they did before; telemetry only observes them (via exception filters, so stack traces are preserved).
- Sync wrappers (`AddReplace`, `Remove`, `Clear`, `Prepopulate`) delegate to their async counterparts and are recorded once. `GetOrAdd` and `AddOrUpdate` record their own operation plus the nested `add_replace` they perform.
- Introspection methods (`Count`, `Oldest`, `Newest`, `All`, `GetKeys`, `GetStatistics`) are not instrumented; their values are covered by the gauges.
- Known behavior made visible, not changed: a non-cancellation exception during an expiration sweep (from the persistence driver's `DeleteAsync` or an `Expired` event handler) ends the expiration task for that cache. The `CachingExpirationStalled` and `CachingExpirationErrors` alerts detect it.
