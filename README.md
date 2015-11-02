# Simple FIFO Cache in C#

The Caching library provides a simple FIFO cache.  It is written in C# and is designed to be thread-safe.

Two projects are included in the solution:

- Caching: the FIFO cache class
- CachingTest: a simple test client

The underlying implementation is a List<Tuple<string, object, DateTime>> with Mutex.

## Usage

Add reference to the Caching DLL and include the Caching namespace:
```
using Caching;
```

Initialize the cache:
```
FIFOCache cache = new FIFOCache(capacity, evict_count, cache_debug);
// capacity (int) is the maximum number of entries
// evict_count (int) is the number to remove when the cache filles
// cache_debug (boolean) enables a LOT of Console.WriteLine statements
```

Add an item to the cache:
```
cache.add_replace(key, data);
// key (string) is a unique identifier
// data (object) is whatever data you like
```

Get an item from the cache:
```
object data = cache.get(key);
// returns null if not present
```

Remove an item from the cache:
```
cache.remove(key);
```

Other helpful methods:
```
string oldest_key = cache.oldest();
string newest_key = cache.newest();
int num_entries = cache.count();
cache.clear();
```
