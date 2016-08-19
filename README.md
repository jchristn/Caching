# Simple FIFO and LRU Cache

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/Caching.dll/
[nuget-img]: https://badge.fury.io/nu/Object.svg

The Caching library provides a simple implementation of a FIFO cache (first-in-first-out) and two LRU (least-recently-used) caches, one based on a List and the second based on a BTree (CSharpTest.Net.Collections).  It is written in C# and is designed to be thread-safe.

Two projects are included in the solution:

- Caching: the cache classes
- CachingTest: a simple test client

Three caches are included; the use case for each is also listed:

- FIFOCache - uses a List<Tuple>, only for small caches (10s of thousands of entries)
- LRUCache - uses a List<Tuple>, only for small caches (10s of thousands of entries)
- LRUCacheBTree - uses a BTree (from CSharpTest.Net.Collections), suitable for larger cache sizes

The underlying implementations are:
```
// FIFOCache
List<Tuple<string, object, DateTime>>             // key, data, added

// LRUCache
List<Tuple<string, object, DateTime, DateTime>>   // key, data, added, lastUsed

// LRUCacheBTree (refer to CSharpTest.Net.Collections)
BPlusTree<string, Tuple<object, DateTime, DateTime>>   // key, <data, added, lastUsed>
```

## Usage

Add reference to the Caching DLL and include the Caching namespace:
```
using Caching;
```

Initialize the desired cache:
```
FIFOCache cache = new FIFOCache(capacity, evictCount, debug);
LRUCache = new LRUCache(capacity, evictCount, debug)
LRUCacheBTree cache = new LRUCacheBTree(capacity, evictCount, debug);

// capacity (int) is the maximum number of entries
// evictCount (int) is the number to remove when the cache fills
// debug (boolean) enables console logging (use sparingly)
```

Add an item to the cache:
```
cache.AddReplace(key, data);
// key (string) is a unique identifier
// data (object) is whatever data you like
```

Get an item from the cache:
```
object data = cache.Get(key);
// returns null if not present
```

Remove an item from the cache:
```
cache.Remove(key);
```

Other helpful methods:
```
string oldestKey = cache.Oldest();
string newestKey = cache.Newest();
string lastUsed = cache.LastUsed();  	// only on LRUCache
string firstUsed = cache.FirstUsed();   // only on LRUCache
int numEntries = cache.Count();
cache.Clear();
```
