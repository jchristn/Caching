# Change Log

## v5.1.1

### Bug Fixes
- **Expiration task resilience**: A non-cancellation exception from the persistence driver's `DeleteAsync` or from an `Expired` event handler previously ended the background expiration task for that cache permanently, and skipped the remaining entries in that sweep. Each expired entry is now processed independently, failures are recorded (`caching.errors{cache.component="expiration"}`, sweep `outcome="error"`), and the task keeps running.
- **Sync GetOrAdd/AddOrUpdate lock scope**: The synchronous `GetOrAdd` and `AddOrUpdate` ran the caller's factory, the persistence write, and `Added`/`Replaced`/`Evicted` handlers while holding the internal cache lock, blocking every other cache operation and deadlocking if that code waited on another thread using the cache. They now match the async variants: the lookup holds the cache lock briefly, and the factory, persistence, and events run outside it. GetOrAdd and AddOrUpdate remain serialized with each other.
- **ExpirationIntervalMs takes effect immediately**: Setting `ExpirationIntervalMs` after construction (the only way to set it) only applied after the pending wait on the previous interval (1000ms by default) elapsed. The pending wait is now re-evaluated against the new interval.

### Tests
- New Touchstone `Resilience` suite (5 cases) reproducing each bug.

## v5.1.0

### New Features
- **Built-in telemetry**: Metrics and traces through the BCL `Meter` and `ActivitySource`, both named `Caching`. No OpenTelemetry or exporter dependency; effectively free until a collector subscribes. See `TELEMETRY.md`.
- **Metrics**: `caching.operation.duration` (per operation and outcome), `caching.lookups` (hit/miss), `caching.evictions` (by reason), `caching.expirations`, `caching.prepopulated`, `caching.errors` (by component and error type), `caching.persistence.duration` and `caching.persistence.calls`, `caching.expiration.sweep.duration` and `caching.expiration.sweep.last_success`, `caching.lock.wait.duration` and `caching.lock.waiting`, and gauges for entries, capacity, evict count, memory usage and limit, expiration interval, and build info.
- **Traces**: spans for every mutation, `stage:lock_wait` and `stage:value_factory` stages inside `GetOrAdd`/`AddOrUpdate`, a client span per persistence driver call, and a root span (linked to the creating trace) per expiration sweep that removed entries. Optional lookup spans via `Telemetry.TraceLookups`.
- **`CacheBase.Name`**: cache name used as the `cache.name` label.
- **`CacheBase.Telemetry`**: per-instance `CacheTelemetrySettings` (`Enable`, `EnableMetrics`, `EnableTraces`, `TraceLookups`, `RecordExceptionMessages`).
- **`CacheTelemetryNames`**: public constants for every meter, source, instrument, attribute, and span name.

### Dependencies
- Adds `System.Diagnostics.DiagnosticSource` 10.0.12 on `netstandard2.0`, `netstandard2.1`, and `net8.0` (in-box on `net10.0`).

### Tests
- New Touchstone `Telemetry` suite (17 cases) using in-memory `MeterListener`/`ActivityListener` capture, covering every instrument, span, failure path, cancellation, context propagation, and the no-listener path.

## v5.0.1

### Bug Fixes
- **Sliding expiration**: Refreshes entries using the original TTL instead of growing the expiration window on each access.
- **Concurrent GetOrAdd**: Ensures `GetOrAdd` and `GetOrAddAsync` only invoke the factory once for the same key under contention.
- **Concurrent AddOrUpdate**: Prevents lost updates when multiple callers update the same key concurrently.
- **Memory tracking**: `Prepopulate()` now updates `CurrentMemoryBytes` and honors `MaxMemoryBytes`.
- **Disposal consistency**: Read-only cache APIs now consistently throw `ObjectDisposedException` after disposal.

### Tests
- Migrated automated coverage to Touchstone shared descriptors with CLI, xUnit, and NUnit runners.

## v5.0.0 (Breaking Changes)

### Breaking Changes
- **Async-only persistence**: `IPersistenceDriver<T1, T2>` now uses async methods only (`WriteAsync`, `GetAsync`, `DeleteAsync`, `ClearAsync`, `ExistsAsync`, `EnumerateAsync`)
- **TryRemove signature**: Changed from `bool TryRemove(T1 key)` to `bool TryRemove(T1 key, out T2 val)` to return the removed value
- **Removed**: `IPersistenceDriverAsync` interface (consolidated into `IPersistenceDriver`)
- **Removed**: Deprecated `ICache<T1, T2>` type alias

### New Features
- **Async cache methods**: `AddReplaceAsync`, `GetOrAddAsync`, `AddOrUpdateAsync`, `RemoveAsync`, `ClearAsync`, `PrepopulateAsync`
- **GetOrDefault**: Returns default value instead of throwing when key not found
- **IEqualityComparer support**: Constructor parameter for custom key comparison
- **TimeSpan overloads**: `AddReplace(key, value, TimeSpan)` for relative expiration

### Bug Fixes
- **Thread-safe disposal**: `Dispose()` now holds lock for entire cleanup operation
- **Prepopulate race condition**: Fixed potential exception when another thread adds the same key during prepopulation

### Other
- Targets .NET 8.0 and .NET 10.0
- Improved exception handling in `TryAddReplace` (catches specific exceptions, not bare `Exception`)

## v4.0.0

- Expiration attribute for cached entries
- Statistics tracking (hit/miss counts, eviction counts, hit rate)
- Memory-based eviction with `MaxMemoryBytes` and `SizeEstimator`
- `GetOrAdd` and `AddOrUpdate` patterns
- Sliding expiration support
- More flexible persistence driver (interface instead of abstract class)

## v3.1.x

- `TryRemove` API
- Dependency updates
- Retargeting

## v3.0.x

- Breaking changes due to major code cleanup
- Allowed persistence driver to support non-string keys
- Prepopulation of persistence layer now a separate method `.Prepopulate()`
- Clearing the cache will now also clear the entire persistence layer

## v2.0.x

- Added persistence, prepopulation, and events
- `TryAddReplace` method

## v1.4.0

- `All()` API

## v1.3.5

- .NET 5.0 support
- IDisposable

## v1.3.4

- XML documentation
