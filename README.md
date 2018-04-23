# Simple FIFO and LRU Cache

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/Caching.dll/
[nuget-img]: https://badge.fury.io/nu/Object.svg

The Caching library provides a simple implementation of a FIFO cache (first-in-first-out) and two LRU (least-recently-used) caches, one based on a dictionary and the second based on a BTree (CSharpTest.Net.Collections).  It is written in C# and is designed to be thread-safe.

Two projects are included in the solution:

- Caching: the cache classes
- CachingTest: a simple test client

Three caches are included; the use case for each is also listed:

- FIFOCache - First-in, first-out cache using a dictionary internally
- LRUCache - Least-recently used cache with a dictionary internally 
- LRUCacheBTree - uses a BTree (from CSharpTest.Net.Collections), suitable for larger cache sizes
 
## Usage

Add reference to the Caching DLL and include the Caching namespace:
```
using Caching;
```

Initialize the desired cache:
```
class Person
{
  public string FirstName;
  public string LastName;
}

FIFOCache<Person> cache = new FIFOCache<Person>(capacity, evictCount, debug);
LRUCache<Person> cache = new LRUCache<Person>(capacity, evictCount, debug)
LRUCacheBTree<Person> cache = new LRUCacheBTree<Person>(capacity, evictCount, debug);

// capacity (int) is the maximum number of entries
// evictCount (int) is the number to remove when the cache reaches capacity
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
Person data = cache.Get(key);
// throws KeyNotFoundException if not present
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
List<string> keys = cache.GetKeys();
cache.Clear();
```
