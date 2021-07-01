# Caching.dll

Simple, fast, effective FIFO and LRU Cache.

 [![NuGet Version](https://img.shields.io/nuget/v/Caching.dll.svg?style=flat)](https://www.nuget.org/packages/Caching.dll/) [![NuGet](https://img.shields.io/nuget/dt/Caching.dll.svg)](https://www.nuget.org/packages/Caching.dll) 

The Caching library provides a simple implementation of a FIFO cache (first-in-first-out) and an LRU (least-recently-used) cache.  It is written in C# and is designed to be thread-safe.
 
## Usage

Add reference to the Caching DLL and include the Caching namespace:
```csharp
using Caching;
```

Initialize the desired cache:
```csharp
class Person
{
  public string FirstName;
  public string LastName;
}

FIFOCache<string, Person> cache = new FIFOCache<string, Person>(capacity, evictCount);
LRUCache<string, Person> cache = new LRUCache<string, Person>(capacity, evictCount) 

// T1 is the type of the key
// T2 is the type of the value
// capacity (int) is the maximum number of entries
// evictCount (int) is the number to remove when the cache reaches capacity
// debug (boolean) enables console logging (use sparingly)
```

Add an item to the cache:
```csharp
cache.AddReplace(key, data);
// key (T1) is a unique identifier
// data (T2) is whatever data you like
```

Get an item from the cache:
```csharp
Person data = cache.Get(key);
// throws KeyNotFoundException if not present

if (!cache.TryGet(key, out data)) 
{ 
  // handle errors 
}
else { 
  // use your data! 
}
```

Remove an item from the cache:
```csharp
cache.Remove(key);
```

Other helpful methods:
```csharp
T1 oldestKey = cache.Oldest();
T1 newestKey = cache.Newest();
T1 lastUsed = cache.LastUsed();  	// only on LRUCache
T1 firstUsed = cache.FirstUsed();   // only on LRUCache
int numEntries = cache.Count();
List<T1> keys = cache.GetKeys();
cache.Clear();
```

Retrieve all cached contents (while preserving the cache):
````csharp
Dictionary<T1, T2> dump = cache.All();
```