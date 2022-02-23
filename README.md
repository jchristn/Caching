# Caching.dll

Simple, fast, effective FIFO and LRU Cache with events and persistence.

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

Alternatively:
```csharp
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
```csharp
Dictionary<T1, T2> dump = cache.All();
```

## Persistence

If you wish to include a persistence layer with the cache, i.e. to store and manage cached objects on another repository in addition to memory:

1) Implement the ```PersistenceDriver``` class; refer to the ```Test.Persistence``` project for a sample implementation that uses a directory on the local hard drive
2) Instantiate the cache (```FIFOCache``` or ```LRUCache```) and pass the instance of the ```PersistenceDriver``` into the constructor
3) An optional Boolean constructor parameter indicates if the cache should be prepopulated with objects found in the ```PersistenceDriver```; this is helpful for cache recovery
4) Persistence *only* works if the key is a ```string```

```csharp
// implementation of PersistenceDriver
public class Persistence : PersistenceDriver
{
  public override void Delete(string key) { ... }
  public override bool Exists(string key) { ... }
  public override byte[] Get(string key) { ... }
  public override void Write(string key, byte[] data) { ... }
  public override byte[] ToBytes(object data) { ... }
  public override T FromBytes<T>(byte[] data) { ... }
  public override List<string> Enumerate() { ... }
}

// instantiate the cache
Persistence persistence = new Persistence();
LRUCache<string, byte[]> cache = new LRUCache<string, byte[]>(capacity, evictCount, persistence);
```

As objects are written to the cache, they are added to persistent storage through the ```Write``` method.  When they are removed or evicted, they are eliminated via the ```Delete``` method.

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