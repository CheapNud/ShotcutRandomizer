using CheapShotcutRandomizer.Models;
using CheapHelpers.Services.DataExchange.Xml;

namespace CheapShotcutRandomizer.Services;

public class ShotcutService(IXmlService xmlService)
{
    private readonly IXmlService _xmlService = xmlService;

    public async Task<Mlt?> LoadProjectAsync(string path)
    {
        try
        {
            return await _xmlService.DeserializeAsync<Mlt>(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading project: {ex.Message}");
            return null;
        }
    }

    public async Task<string> SaveProjectAsync(Mlt project, string originalPath)
    {
        try
        {
            var newpath = Path.Combine(
                Path.GetDirectoryName(originalPath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(originalPath)}.Random{Guid.NewGuid().ToString()[..4]}{Path.GetExtension(originalPath)}"
            );

            await _xmlService.SerializeAsync(newpath, project);

            return newpath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving project: {ex.Message}");
            throw;
        }
    }

    public void ShufflePlaylist(Mlt project, int playlistIndex)
    {
        if (playlistIndex < 0 || playlistIndex >= project.Playlist.Count)
            throw new ArgumentOutOfRangeException(nameof(playlistIndex));

        project.Playlist[playlistIndex].Blank = [];
        project.Playlist[playlistIndex].Entry = [.. project.Playlist[playlistIndex].Entry.Shuffle()];
    }

    public Playlist GenerateRandomPlaylist(
        Mlt project,
        List<(int PlaylistIndex, int TargetDurationSeconds)> sourcePlaylists,
        double durationWeight,
        double numberOfVideosWeight)
    {
        var allVideos = new List<Entry>();
        int totalTargetDuration = 0;

        foreach (var (playlistIndex, targetDuration) in sourcePlaylists)
        {
            if (playlistIndex < 0 || playlistIndex >= project.Playlist.Count)
                continue;

            int actualTarget = targetDuration == 0
                ? project.Playlist[playlistIndex].Entry.Sum(x => x.Duration)
                : targetDuration;

            totalTargetDuration += actualTarget;

            var selectedVideos = new SimulatedAnnealingVideoSelector(actualTarget, durationWeight, numberOfVideosWeight)
                .SelectVideos([.. project.Playlist[playlistIndex].Entry.Shuffle()])
                .Shuffle()
                .ToList();

            allVideos.AddRange(selectedVideos);
        }

        var finalVideos = new SimulatedAnnealingVideoSelector(totalTargetDuration, durationWeight, numberOfVideosWeight)
            .SelectVideos(allVideos)
            .Shuffle()
            .ToList();

        var newPlaylist = new Playlist
        {
            Entry = finalVideos,
            Id = $"playlist{project.Playlist.Count + 1}",
            Property =
            [
                new() { Name = "shotcut:video", Text = "1" },
                new() { Name = "shotcut:name", Text = "generated" }
            ]
        };

        project.Playlist.Add(newPlaylist);
        project.Tractor.First(x => x.Property.Any(y => y.Name == "shotcut"))
            .Track.Add(new Track { Producer = newPlaylist.Id });

        return newPlaylist;
    }
}
