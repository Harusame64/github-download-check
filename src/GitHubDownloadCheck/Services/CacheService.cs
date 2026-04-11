using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitHubDownloadCheck.Models;

namespace GitHubDownloadCheck.Services;

public class CacheService
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitHubDownloadCheck");

    private static readonly string CachePath = Path.Combine(AppDataDir, "cache.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AppCache Load()
    {
        if (!File.Exists(CachePath))
            return new AppCache();

        try
        {
            var json = File.ReadAllText(CachePath);
            return JsonSerializer.Deserialize<AppCache>(json, JsonOptions) ?? new AppCache();
        }
        catch
        {
            return new AppCache();
        }
    }

    public void SaveSnapshot(string repoKey, List<GitHubRelease> releases)
    {
        Directory.CreateDirectory(AppDataDir);
        var cache = Load();

        if (!cache.Repositories.TryGetValue(repoKey, out var repoCache))
        {
            repoCache = new RepositoryCache();
            cache.Repositories[repoKey] = repoCache;
        }

        repoCache.Snapshots.Add(new DownloadSnapshot
        {
            FetchedAt = DateTime.UtcNow,
            Releases = releases
        });

        // 最大50スナップショットを保持
        if (repoCache.Snapshots.Count > 50)
            repoCache.Snapshots.RemoveAt(0);

        var json = JsonSerializer.Serialize(cache, JsonOptions);
        File.WriteAllText(CachePath, json);
    }

    public List<DownloadSnapshot> GetSnapshots(string repoKey)
    {
        var cache = Load();
        return cache.Repositories.TryGetValue(repoKey, out var repoCache)
            ? repoCache.Snapshots
            : new List<DownloadSnapshot>();
    }
}
