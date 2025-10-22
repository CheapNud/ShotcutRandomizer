using CheapShotcutRandomizer.Models;

namespace CheapShotcutRandomizer.Services;

/// <summary>
/// Service for searching files within MLT project tracks
/// </summary>
public class FileSearchService
{
    /// <summary>
    /// Get the resource path from a producer
    /// </summary>
    private static string? GetProducerResourcePath(Producer producer)
    {
        var resourceProp = producer.Property?.FirstOrDefault(p => p.Name == "resource");
        return resourceProp?.Text;
    }

    /// <summary>
    /// Search for files matching a partial filename across all playlists
    /// </summary>
    public List<FileSearchResult> SearchByPartialFilename(Mlt project, string partialFilename, List<int>? playlistIndices = null)
    {
        var results = new List<FileSearchResult>();

        if (string.IsNullOrWhiteSpace(partialFilename))
            return results;

        // Build producer ID to file path mapping
        var producerPaths = new Dictionary<string, string>();
        if (project.Producer != null)
        {
            var path = GetProducerResourcePath(project.Producer);
            if (path != null)
                producerPaths[project.Producer.Id] = path;
        }

        // Search in playlists (playlists are also producers in MLT)
        var playlistsToSearch = playlistIndices ?? Enumerable.Range(0, project.Playlist.Count).ToList();

        foreach (var playlistIndex in playlistsToSearch)
        {
            if (playlistIndex < 0 || playlistIndex >= project.Playlist.Count)
                continue;

            var playlist = project.Playlist[playlistIndex];

            for (int entryIndex = 0; entryIndex < playlist.Entry.Count; entryIndex++)
            {
                var entry = playlist.Entry[entryIndex];
                var producerId = entry.Producer;

                // Try to find the producer's resource path
                string? resourcePath = null;

                // Check if we already have it
                if (producerPaths.TryGetValue(producerId, out var cachedPath))
                {
                    resourcePath = cachedPath;
                }
                else
                {
                    // Search for the producer definition
                    // In MLT, producers might be defined at project level or in chains
                    if (project.Chain != null)
                    {
                        foreach (var chain in project.Chain)
                        {
                            if (chain.Id == producerId)
                            {
                                var resProp = chain.Property?.FirstOrDefault(p => p.Name == "resource");
                                if (resProp != null)
                                {
                                    resourcePath = resProp.Text;
                                    producerPaths[producerId] = resourcePath;
                                    break;
                                }
                            }
                        }
                    }
                }

                // Check if this resource matches the search
                if (resourcePath != null && resourcePath.Contains(partialFilename, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new FileSearchResult
                    {
                        ProducerId = producerId,
                        FilePath = resourcePath,
                        PlaylistIndex = playlistIndex,
                        PlaylistName = playlist.Name,
                        EntryIndex = entryIndex,
                        EntryInPoint = entry.In,
                        EntryOutPoint = entry.Out
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Search for a specific producer ID across playlists
    /// </summary>
    public List<FileSearchResult> SearchByProducerId(Mlt project, string producerId, List<int>? playlistIndices = null)
    {
        var results = new List<FileSearchResult>();
        var playlistsToSearch = playlistIndices ?? Enumerable.Range(0, project.Playlist.Count).ToList();

        foreach (var playlistIndex in playlistsToSearch)
        {
            if (playlistIndex < 0 || playlistIndex >= project.Playlist.Count)
                continue;

            var playlist = project.Playlist[playlistIndex];

            for (int entryIndex = 0; entryIndex < playlist.Entry.Count; entryIndex++)
            {
                var entry = playlist.Entry[entryIndex];

                if (entry.Producer == producerId)
                {
                    results.Add(new FileSearchResult
                    {
                        ProducerId = producerId,
                        PlaylistIndex = playlistIndex,
                        PlaylistName = playlist.Name,
                        EntryIndex = entryIndex,
                        EntryInPoint = entry.In,
                        EntryOutPoint = entry.Out
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Get all unique files used in specified playlists
    /// </summary>
    public List<UniqueFileInfo> GetUniqueFiles(Mlt project, List<int>? playlistIndices = null)
    {
        var producerIds = new HashSet<string>();
        var playlistsToSearch = playlistIndices ?? Enumerable.Range(0, project.Playlist.Count).ToList();

        // Collect all unique producer IDs
        foreach (var playlistIndex in playlistsToSearch)
        {
            if (playlistIndex < 0 || playlistIndex >= project.Playlist.Count)
                continue;

            var playlist = project.Playlist[playlistIndex];
            foreach (var entry in playlist.Entry)
            {
                producerIds.Add(entry.Producer);
            }
        }

        // Map producer IDs to file paths
        var results = new List<UniqueFileInfo>();

        foreach (var producerId in producerIds)
        {
            string? filePath = null;

            // Try to find file path from chains
            if (project.Chain != null)
            {
                var chain = project.Chain.FirstOrDefault(c => c.Id == producerId);
                if (chain != null)
                {
                    var resProp = chain.Property?.FirstOrDefault(p => p.Name == "resource");
                    filePath = resProp?.Text;
                }
            }

            // Try from main producer
            if (filePath == null && project.Producer?.Id == producerId)
            {
                var resProp = project.Producer.Property?.FirstOrDefault(p => p.Name == "resource");
                filePath = resProp?.Text;
            }

            var usageCount = 0;
            foreach (var playlistIndex in playlistsToSearch)
            {
                if (playlistIndex >= 0 && playlistIndex < project.Playlist.Count)
                {
                    usageCount += project.Playlist[playlistIndex].Entry.Count(e => e.Producer == producerId);
                }
            }

            results.Add(new UniqueFileInfo
            {
                ProducerId = producerId,
                FilePath = filePath ?? $"[Unknown: {producerId}]",
                UsageCount = usageCount
            });
        }

        return results.OrderBy(f => Path.GetFileName(f.FilePath)).ToList();
    }
}

/// <summary>
/// Result of a file search operation
/// </summary>
public class FileSearchResult
{
    public string ProducerId { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int PlaylistIndex { get; set; }
    public string PlaylistName { get; set; } = string.Empty;
    public int EntryIndex { get; set; }
    public string EntryInPoint { get; set; } = string.Empty;
    public string EntryOutPoint { get; set; } = string.Empty;

    public string DisplayName => FilePath != null ? Path.GetFileName(FilePath) : ProducerId;
}

/// <summary>
/// Information about a unique file in the project
/// </summary>
public class UniqueFileInfo
{
    public string ProducerId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int UsageCount { get; set; }

    public string FileName => Path.GetFileName(FilePath);
}
