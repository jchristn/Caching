# Change Log

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
