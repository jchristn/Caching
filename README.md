# Simple FIFO and LRU Cache

<<<<<<< HEAD
The Caching library provides a simple implementation of a FIFO cache (first-in-first-out) and two LRU (least-recently-used) caches, one based on a List and the second based on a BTree (CSharpTest.Net.Collections).  It is written in C# and is designed to be thread-safe.
=======
The Caching library provides a simple implementation of a FIFO cache (first-in-first-out) and a LRU (least-recently-used) cache.  It is written in C# and is designed to be thread-safe.  The underlying implementation uses a list, which is appropriate for small record counts.  If you require a solution for large record counts, I recommend using something based on a B-Tree.  
>>>>>>> 14fc047cbfa362feceeada1aa78c0abff2a69f2d

Two projects are included in the solution:

- Caching: the FIFO cache class
- CachingTest: a simple test client

The underlying implementations are:
```
// Small FIFO Cache
List<Tuple<string, object, DateTime>>             // key, data, added

// Small LRU Cache
List<Tuple<string, object, DateTime, DateTime>>   // key, data, added, last_used

// Larger LRU Cache
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
