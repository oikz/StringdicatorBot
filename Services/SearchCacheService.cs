using System;
using System.Threading.Tasks;
using GoogleApi.Entities.Search.Common;
using Microsoft.Extensions.Caching.Memory;
using Stringdicator.Modules;

namespace Stringdicator.Services;

/// <summary>
/// A static Cache for storing image search requests so that each request can be reused for up to 10 different images.
/// Prevents excessive quota use with the "next image" button.
/// </summary>
public class ImageSearchCacheService {
    /// <summary>
    /// In memory cache to store the results.
    /// </summary>
    private readonly MemoryCache _cache = new(new MemoryCacheOptions {
        SizeLimit = 128
    });

    /// <summary>
    /// Get an array of Items from the cache if searched before or perform an Image Search, caching and returning the
    /// result.
    /// </summary>
    /// <param name="stringModule">The Module that called this function and is required for Image Searching</param>
    /// <param name="searchTerm">The search term that was used</param>
    /// <param name="startIndex">The starting index that the search started at</param>
    /// <returns>An array of Items from the cache containing the Image Search results</returns>
    public async Task<Item[]> GetOrCreate(StringModule stringModule, string searchTerm, int startIndex) {
        await stringModule.DeferAsync();
        if (_cache.TryGetValue($"Term-{searchTerm},Index-{startIndex}", out Item[] cacheEntry)) return cacheEntry;

        cacheEntry = await stringModule.ImageSearch(searchTerm, startIndex);

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetSlidingExpiration(TimeSpan.FromMinutes(5))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

        if (cacheEntry.Length != 0) _cache.Set($"Term-{searchTerm},Index-{startIndex}", cacheEntry, cacheEntryOptions);
        return cacheEntry;
    }
}