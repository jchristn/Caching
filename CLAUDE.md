# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A C# NuGet package providing thread-safe FIFO (first-in-first-out) and LRU (least-recently-used) cache implementations with support for events, persistence, and expiration.

**Targets:** .NET Standard 2.0/2.1, .NET 6.0, .NET 8.0

## Build and Test Commands

```bash
# Build the entire solution
dotnet build src/Caching.sln

# Build in Release mode (creates NuGet package)
dotnet build src/Caching.sln --configuration Release

# Build only the library
dotnet build src/Caching/Caching.csproj

# Run the basic test application
dotnet run --project src/Test/Test.csproj

# Run the persistence test application
dotnet run --project src/Test.Persistence/Test.Persistence.csproj

# Run the events test application
dotnet run --project src/Test.Events/Test.Events.csproj
```

## Architecture

### Core Components

**Cache Implementations** (`FIFOCache<T1, T2>`, `LRUCache<T1, T2>`)
- Both inherit from abstract base class `ICache<T1, T2>` (src/Caching/ICache.cs:11)
- T1 is the key type, T2 is the value type
- Thread-safe via internal `_CacheLock` object
- Internally store data in `Dictionary<T1, DataNode<T2>>` (src/Caching/ICache.cs:96)
- Both run a background `ExpirationTask` for automatic expiration handling (src/Caching/ICache.cs:99)

**DataNode** (src/Caching/DataNode.cs:9)
- Wraps cached values with metadata: `Added`, `LastUsed`, `Expiration`
- Tracks when entries were added and last accessed

**Key Behavioral Differences:**
- **FIFO**: Eviction is based on `Added` timestamp - oldest entries are removed first regardless of access patterns (src/Caching/FIFOCache.cs:278)
- **LRU**: Eviction is based on `LastUsed` timestamp - least recently accessed entries are removed first

### Persistence Layer

**IPersistenceDriver<T1, T2>** (src/Caching/IPersistenceDriver.cs:8)
- Abstract class that must be implemented for custom persistence
- Called synchronously during cache operations: `Write()` on add, `Delete()` on remove/evict, `Clear()` on clear
- Reference implementation: `Test.Persistence.PersistenceDriver` (src/Test.Persistence/PersistenceDriver.cs:8) - simple file-based storage

### Events System

**CacheEvents<T1, T2>** (src/Caching/CacheEvents.cs)
- Provides event hooks for cache operations: `Added`, `Removed`, `Replaced`, `Evicted`, `Expired`, `Prepopulated`, `Cleared`, `Disposed`
- Events are invoked synchronously after the operation completes
- Access via `cache.Events.EventName += handler`

### Expiration Mechanism

- Background task runs every `ExpirationIntervalMs` (default: 1000ms) checking for expired entries (src/Caching/ICache.cs:74)
- Expired entries trigger the `Expired` event and are removed automatically (src/Caching/FIFOCache.cs:435)
- Expiration timestamps must be in UTC and in the future

## Code Patterns

**Eviction Logic:**
- When capacity is reached, the oldest (FIFO) or least recently used (LRU) `EvictCount` entries are removed
- Uses LINQ `.OrderBy()` and `.Skip()` to select entries to retain (src/Caching/FIFOCache.cs:278)
- Persistence layer is notified of all deletions

**Thread Safety:**
- All public methods use `lock (_CacheLock)` for synchronization
- Get operations update `LastUsed` timestamp within the lock

**Try Pattern:**
- Methods with `Try` prefix (e.g., `TryGet`, `TryAddReplace`, `TryRemove`) return bool instead of throwing exceptions
- Implemented as simple exception wrappers around the throwing variants

## NuGet Package Details

- Package ID: `Caching`
- Current version: 3.1.3 (see src/Caching/Caching.csproj:7)
- Package is generated on Release builds (`GeneratePackageOnBuild`)
- XML documentation is auto-generated (Caching.xml)
