# Simple FIFO and LRU Cache

The Caching library provides a simple implementation of a FIFO cache (first-in-first-out) and two LRU (least-recently-used) caches, one based on a List and the second based on a BTree (CSharpTest.Net.Collections).  It is written in C# and is designed to be thread-safe.

Two projects are included in the solution:

- Caching: the FIFO cache class
- CachingTest: a simple test client

Three caches are included; the use case for each is also listed:

- FIFOCache - uses a List<Tuple>, only for small caches (10s of thousands of entries)
- LRUCache - uses a List<Tuple>, only for small caches (10s of thousands of entries)
- LRUCacheBTree - uses a BTree, suitable for larger cache sizes

The underlying implementations are:
```
// FIFOCache
List<Tuple<string, object, DateTime>>             // key, data, added

// LRUCache
List<Tuple<string, object, DateTime, DateTime>>   // key, data, added, last_used

// LRUCacheBTree
BPlusTree<string, Tuple<object, DateTime, DateTime>>   // key, <data, added, last_used>
```

## Usage

Add reference to the Caching DLL and include the Caching namespace:
```
using Caching;
```

Initialize the desired cache:
```
FIFOCache cache = new FIFOCache(capacity, evict_count, cache_debug);
LRUCache = new LRUCache(capacity, evict_count, cache_debug)
LRUCacheBTree cache = new LRUCacheBTree(capacity, evict_count, cache_debug);

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
string last_used = cache.last_used();  // only on LRUCache
string first_used = cache.first_used();  // only on LRUCache
int num_entries = cache.count();
cache.clear();
```
