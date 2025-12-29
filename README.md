# Caching

<img src="https://github.com/jchristn/Caching/raw/master/assets/icon.png" height="128" width="128">

[![NuGet Version](https://img.shields.io/nuget/v/Caching.svg?style=flat)](https://www.nuget.org/packages/Caching/) [![NuGet](https://img.shields.io/nuget/dt/Caching.svg)](https://www.nuget.org/packages/Caching)

High-performance, thread-safe caching library for .NET with FIFO and LRU eviction policies, automatic expiration, persistence support, and comprehensive event notifications.

## What Is This Library?

Caching is a lightweight, production-ready caching library that provides:

- **FIFO (First-In-First-Out) Cache**: Evicts the oldest entries when capacity is reached
- **LRU (Least Recently Used) Cache**: Evicts the least recently accessed entries
- **Thread-Safe**: All operations are fully thread-safe for concurrent access
- **Automatic Expiration**: Time-based expiration with sliding or absolute TTL
- **Event Notifications**: Comprehensive events for cache operations
- **Persistence Layer**: Optional persistence to disk or custom storage
- **Statistics Tracking**: Built-in hit/miss rates, eviction counts, and performance metrics
- **Memory Limits**: Optional memory-based eviction in addition to count-based
- **Modern API**: GetOrAdd, AddOrUpdate, and async-ready patterns

## Installation

```bash
dotnet add package Caching
```

Or via Package Manager:

```powershell
Install-Package Caching
```

## Quick Start

### Basic FIFO Cache

```csharp
using Caching;

// Create a FIFO cache with capacity of 1000, evicting 100 items when full
var cache = new FIFOCache<string, Person>(capacity: 1000, evictCount: 100);

// Add items
cache.AddReplace("user:123", new Person { Name = "Alice", Age = 30 });

// Get items
Person person = cache.Get("user:123");

// Try pattern (no exceptions)
if (cache.TryGet("user:123", out Person p))
{
    Console.WriteLine($"Found: {p.Name}");
}

// Remove items
cache.Remove("user:123");

// Dispose when done
cache.Dispose();
```

### Basic LRU Cache

```csharp
// LRU evicts least recently accessed items
var cache = new LRUCache<string, byte[]>(capacity: 500, evictCount: 50);

cache.AddReplace("image:1", imageBytes);
cache.Get("image:1"); // Updates last-used timestamp

cache.Dispose();
```

## Key Features

### 1. Expiration

#### Absolute Expiration

```csharp
// Expires at specific time
cache.AddReplace("session:xyz", sessionData, DateTime.UtcNow.AddMinutes(30));

// Or use TimeSpan for relative expiration
cache.AddReplace("temp:data", tempData, TimeSpan.FromSeconds(60));
```

#### Sliding Expiration

```csharp
// Enable sliding expiration (TTL refreshes on access)
cache.SlidingExpiration = true;

cache.AddReplace("sliding:key", value, TimeSpan.FromMinutes(5));
// Each time you access the item, expiration resets to 5 minutes from now
cache.Get("sliding:key"); // Refreshes expiration
```

### 2. GetOrAdd Pattern

```csharp
// Atomically get existing or create new value
var person = cache.GetOrAdd("user:456", key =>
{
    // This factory only runs if key doesn't exist
    return database.GetPerson(456);
});

// With expiration
var data = cache.GetOrAdd("data:789",
    key => LoadExpensiveData(key),
    TimeSpan.FromHours(1));
```

### 3. AddOrUpdate Pattern

```csharp
// Add if new, update if exists
var result = cache.AddOrUpdate(
    "counter:visits",
    addValue: 1,
    updateValueFactory: (key, oldValue) => oldValue + 1);

Console.WriteLine($"Visit count: {result}");
```

### 4. Events

```csharp
cache.Events.Added += (sender, e) => Console.WriteLine($"Added: {e.Key}");

cache.Events.Replaced += (sender, e) => Console.WriteLine($"Replaced: {e.Key}");

cache.Events.Removed += (sender, e) => Console.WriteLine($"Removed: {e.Key}");

cache.Events.Evicted += (sender, keys) => Console.WriteLine($"Evicted {keys.Count} items");

cache.Events.Expired += (sender, key) => Console.WriteLine($"Expired: {key}");

cache.Events.Cleared += (sender, e) => Console.WriteLine("Cache cleared");

cache.Events.Disposed += (sender, e) => Console.WriteLine("Cache disposed");
```

### 5. Persistence

Implement the `IPersistenceDriver<TKey, TValue>` interface (all methods are async):

```csharp
public class FilePersistence : IPersistenceDriver<string, string>
{
    private readonly string _directory;

    public FilePersistence(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task WriteAsync(string key, string data, CancellationToken ct = default)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, key), data, ct);
    }

    public async Task<string> GetAsync(string key, CancellationToken ct = default)
    {
        return await File.ReadAllTextAsync(Path.Combine(_directory, key), ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await Task.Run(() => File.Delete(Path.Combine(_directory, key)), ct);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        foreach (var file in Directory.GetFiles(_directory))
            await Task.Run(() => File.Delete(file), ct);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return await Task.Run(() => File.Exists(Path.Combine(_directory, key)), ct);
    }

    public async Task<List<string>> EnumerateAsync(CancellationToken ct = default)
    {
        return await Task.Run(() => Directory.GetFiles(_directory)
            .Select(Path.GetFileName)
            .ToList(), ct);
    }
}

// Use with cache
var persistence = new FilePersistence("./cache_data");
var cache = new LRUCache<string, string>(1000, 100, persistence);

// Restore from persistence on startup
await cache.PrepopulateAsync();

// All add/remove operations automatically persist
await cache.AddReplaceAsync("key", "value"); // Written to disk
await cache.RemoveAsync("key");              // Deleted from disk

// Sync methods also available (block on async internally)
cache.AddReplace("key2", "value2");
cache.Remove("key2");
```

### 6. Statistics

```csharp
var cache = new FIFOCache<string, object>(1000, 100);

// Perform operations
cache.AddReplace("key1", "value1");
cache.Get("key1");        // Hit
cache.TryGet("missing", out _); // Miss

// Get statistics
var stats = cache.GetStatistics();

Console.WriteLine($"Hit Rate: {stats.HitRate:P}");
Console.WriteLine($"Hits: {stats.HitCount}");
Console.WriteLine($"Misses: {stats.MissCount}");
Console.WriteLine($"Evictions: {stats.EvictionCount}");
Console.WriteLine($"Expirations: {stats.ExpirationCount}");
Console.WriteLine($"Current Count: {stats.CurrentCount}");
Console.WriteLine($"Capacity: {stats.Capacity}");

// Reset counters
cache.ResetStatistics();
```

### 7. Memory Limits

```csharp
var cache = new FIFOCache<string, byte[]>(10000, 100);

// Limit cache to 100MB
cache.MaxMemoryBytes = 100 * 1024 * 1024;

// Provide size estimator for your value type
cache.SizeEstimator = bytes => bytes.Length;

// Cache will evict entries if memory limit is exceeded
cache.AddReplace("large", new byte[10 * 1024 * 1024]); // 10MB

Console.WriteLine($"Memory used: {cache.CurrentMemoryBytes} bytes");
```

### 8. Configuration Options

```csharp
var cache = new LRUCache<int, string>(1000, 100);

// Sliding expiration
cache.SlidingExpiration = true;

// Expiration check interval (default: 1000ms)
cache.ExpirationIntervalMs = 500;

// Memory limits
cache.MaxMemoryBytes = 50 * 1024 * 1024; // 50MB
cache.SizeEstimator = str => str.Length * 2; // Unicode estimation
```

## API Reference

### Core Methods

| Method | Description |
|--------|-------------|
| `AddReplace(key, value, expiration?)` | Add or replace a cache entry |
| `AddReplaceAsync(key, value, expiration?, ct?)` | Async version of AddReplace |
| `Get(key)` | Get value (throws if not found) |
| `GetOrDefault(key, defaultValue?)` | Get value or return default if not found |
| `TryGet(key, out value)` | Try to get value (returns false if not found) |
| `GetOrAdd(key, factory, expiration?)` | Get existing or add new value atomically |
| `GetOrAddAsync(key, asyncFactory, expiration?, ct?)` | Async version of GetOrAdd |
| `AddOrUpdate(key, addValue, updateFactory, expiration?)` | Add new or update existing value |
| `AddOrUpdateAsync(key, addValue, asyncUpdateFactory, expiration?, ct?)` | Async version of AddOrUpdate |
| `Remove(key)` | Remove entry |
| `RemoveAsync(key, ct?)` | Async version of Remove |
| `TryRemove(key, out value)` | Try to remove entry, returns removed value |
| `Contains(key)` | Check if key exists |
| `Clear()` | Remove all entries |
| `ClearAsync(ct?)` | Async version of Clear |
| `Count()` | Get current number of entries |
| `GetKeys()` | Get all keys |
| `All()` | Get all key-value pairs |
| `Oldest()` | Get key of oldest entry |
| `Newest()` | Get key of newest entry |
| `Prepopulate()` | Load from persistence layer |
| `PrepopulateAsync(ct?)` | Async version of Prepopulate |
| `GetStatistics()` | Get cache statistics |
| `ResetStatistics()` | Reset counters |

### Constructors

```csharp
// Basic cache
new FIFOCache<TKey, TValue>(capacity, evictCount);
new LRUCache<TKey, TValue>(capacity, evictCount);

// With custom key comparer
new FIFOCache<TKey, TValue>(capacity, evictCount, comparer: StringComparer.OrdinalIgnoreCase);

// With persistence
new FIFOCache<TKey, TValue>(capacity, evictCount, persistenceDriver);

// With persistence and custom comparer
new LRUCache<TKey, TValue>(capacity, evictCount, persistenceDriver, comparer);
```

### Properties

| Property | Description |
|----------|-------------|
| `Capacity` | Maximum number of entries |
| `EvictCount` | Number of entries to evict when full |
| `ExpirationIntervalMs` | How often to check for expired entries (ms) |
| `SlidingExpiration` | Enable sliding expiration |
| `MaxMemoryBytes` | Maximum memory limit (0 = unlimited) |
| `SizeEstimator` | Function to estimate value size |
| `CurrentMemoryBytes` | Current estimated memory usage |
| `HitCount` | Total cache hits |
| `MissCount` | Total cache misses |
| `EvictionCount` | Total evictions |
| `ExpirationCount` | Total expirations |
| `HitRate` | Cache hit rate (0.0 to 1.0) |
| `KeyComparer` | The equality comparer used for keys |
| `Events` | Event handlers |
| `Persistence` | Persistence driver |

## Thread Safety

All cache operations are thread-safe and can be called concurrently from multiple threads:

```csharp
var cache = new LRUCache<int, string>(10000, 100);

// Safe to call from multiple threads
Parallel.For(0, 1000, i =>
{
    cache.AddReplace(i, $"value{i}");
    cache.TryGet(i, out _);
    if (i % 10 == 0) cache.TryRemove(i, out _);
});
```

## Performance Tips

1. **Choose the Right Cache Type**:
   - Use **FIFO** when access patterns don't matter (e.g., time-series data)
   - Use **LRU** when recent items are more likely to be accessed again

2. **Set Appropriate Capacity**:
   - Monitor `HitRate` to tune capacity
   - Higher capacity = better hit rate but more memory

3. **Tune EvictCount**:
   - Larger `EvictCount` = fewer eviction operations but more items removed at once
   - Smaller `EvictCount` = more frequent evictions but finer-grained

4. **Use TryGet for Optional Lookups**:
   - `TryGet` is faster than catching exceptions from `Get`

5. **Minimize Event Handler Work**:
   - Events fire synchronously; keep handlers fast
   - Offload heavy work to background tasks

6. **Memory Limits**:
   - Only use `MaxMemoryBytes` if needed; it adds overhead
   - Provide accurate `SizeEstimator` for best results

## Migrating from v4.x to v5.0

v5.0 is a breaking change. Key updates required:

### 1. Persistence Driver (Async-Only)

```csharp
// v4.x (sync methods)
public class MyDriver : IPersistenceDriver<string, string>
{
    public void Write(string key, string data) { }
    public string Get(string key) { }
    public void Delete(string key) { }
    public void Clear() { }
    public bool Exists(string key) { }
    public List<string> Enumerate() { }
}

// v5.0 (async methods)
public class MyDriver : IPersistenceDriver<string, string>
{
    public Task WriteAsync(string key, string data, CancellationToken ct = default) { }
    public Task<string> GetAsync(string key, CancellationToken ct = default) { }
    public Task DeleteAsync(string key, CancellationToken ct = default) { }
    public Task ClearAsync(CancellationToken ct = default) { }
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) { }
    public Task<List<string>> EnumerateAsync(CancellationToken ct = default) { }
}
```

### 2. TryRemove Returns Value

```csharp
// v4.x
bool removed = cache.TryRemove("key");

// v5.0
bool removed = cache.TryRemove("key", out string value);
// or if you don't need the value:
bool removed = cache.TryRemove("key", out _);
```

### 3. New Features

```csharp
// Custom key comparer
var cache = new FIFOCache<string, int>(100, 10,
    comparer: StringComparer.OrdinalIgnoreCase);

// GetOrDefault (doesn't throw)
string value = cache.GetOrDefault("missing", "fallback");

// Async methods
await cache.AddReplaceAsync("key", "value");
var result = await cache.GetOrAddAsync("key", async k => await FetchAsync(k));
await cache.PrepopulateAsync();
```

## Migrating from v3.x to v4.0

```csharp
// v3.x
cache.Events.Added = handler; // Overwrites all handlers! ❌

// v4.0+
cache.Events.Added += handler; // Adds handler ✅
```

## Contributing

Contributions are welcome! Please open an issue or PR on GitHub.

## License

See [LICENSE.md](LICENSE.md)
