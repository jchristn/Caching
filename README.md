# Caching

Simple, fast, effective FIFO and LRU Cache with events and persistence.

 [![NuGet Version](https://img.shields.io/nuget/v/Caching.svg?style=flat)](https://www.nuget.org/packages/Caching/) [![NuGet](https://img.shields.io/nuget/dt/Caching.svg)](https://www.nuget.org/packages/Caching) 

This Caching library provides a simple implementation of a FIFO cache (first-in-first-out) and an LRU (least-recently-used) cache.  It is written in C# and is designed to be thread-safe.
 
## New in v3.0.x

- Breaking changes due to major code cleanup
- Allowed persistence driver to support non-string keys
- Prepopulation of persistence layer now a separate method ```.Prepopulate()```
- Clearing the cache will now also clear the entire persistence layer

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

bool success = cache.TryAddReplace(key, data);
if (!success) { ... }
```

Get an item from the cache:
```csharp
Person data = cache.Get(key);
// throws KeyNotFoundException if not present

if (!cache.TryGet(key, out data)) 
{ 
  // handle errors 
}
else 
{ 
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
int numEntries = cache.Count();
List<T1> keys = cache.GetKeys();
cache.Clear();
```

Retrieve all cached contents (while preserving the cache):
```csharp
Dictionary<T1, T2> dump = cache.All();
```

## Persistence

If you wish to include a persistence layer with the cache, i.e. to store and manage cached objects on another repository in addition to memory:

1) Implement the ```IPersistenceDriver``` abstract class; refer to the ```Test.Persistence``` project for a sample implementation that uses a directory on the local hard drive
2) Instantiate the cache (```FIFOCache``` or ```LRUCache```) and pass the instance of the ```IPersistenceDriver``` into the constructor
3) If you wish to prepopulate the cache from the persistence driver, call ```.Prepopulate()``` before using the cache.
4) If you clear the cache, the persistence layer will also be cleared.

```csharp
// implementation of PersistenceDriver
public class MyPersistenceDriver : IPersistenceDriver
{
  public override void Delete(string key) { ... }
  public override void Clear() { ... }
  public override bool Exists(string key) { ... }
  public override byte[] Get(string key) { ... }
  public override void Write(string key, byte[] data) { ... }
  public override byte[] ToBytes(object data) { ... }
  public override T FromBytes<T>(byte[] data) { ... }
  public override List<string> Enumerate() { ... }
}

// instantiate the cache
MyPersistenceDriver persistence = new MyPersistenceDriver();
LRUCache<string, byte[]> cache = new LRUCache<string, byte[]>(capacity, evictCount, persistence);
cache.Prepopulate();
```

As objects are written to the cache, they are added to persistent storage through the ```Write``` method.  When they are removed or evicted, they are eliminated via the ```Delete``` method.  When the cache is cleared, the persistence layer is also cleared.

## Events

If you wish to invoke events when certain cache actions are taken, attach event handlers to the entries found in ```FIFOCache.Events``` or ```LRUCache.Events```.  These events are invoked synchronously after the associated cache operation.

```csharp
FIFOCache<string, byte[]> cache = new FIFOCache<string, byte[]>(_Capacity, _EvictCount);
cache.Events.Added += Added;
cache.Events.Cleared += Cleared;
cache.Events.Disposed += Disposed;
cache.Events.Evicted += Evicted;
cache.Events.Prepopulated += Prepopulated;
cache.Events.Removed += Removed;
cache.Events.Replaced += Replaced;

static void Replaced(object sender, DataEventArgs<string, byte[]> e)
{
    Console.WriteLine("*** Cache entry " + e.Key + " replaced");
}

static void Removed(object sender, DataEventArgs<string, byte[]> e)
{
    Console.WriteLine("*** Cache entry " + e.Key + " removed");
}

static void Prepopulated(object sender, DataEventArgs<string, byte[]> e)
{
    Console.WriteLine("*** Cache entry " + e.Key + " prepopulated from persistent storage");
}

static void Evicted(object sender, List<string> e)
{
    Console.WriteLine("*** Eviction event involving " + e.Count + " entries");
    foreach (string curr in e) Console.WriteLine("    | " + curr);
}

static void Disposed(object sender, EventArgs e)
{
    Console.WriteLine("*** Disposed");
}

static void Cleared(object sender, EventArgs e)
{
    Console.WriteLine("*** Cache cleared");
}

static void Added(object sender, DataEventArgs<string, byte[]> e)
{
    Console.WriteLine("*** Cache entry " + e.Key + " added");
}
```

## Version History

Refer to ```CHANGELOG.md``` for version history.
