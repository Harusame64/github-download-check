using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GitHubDownloadCheck.Models;

public class DownloadSnapshot
{
    [JsonPropertyName("fetchedAt")]
    public DateTime FetchedAt { get; set; }

    [JsonPropertyName("releases")]
    public List<GitHubRelease> Releases { get; set; } = new();
}

public class RepositoryCache
{
    [JsonPropertyName("snapshots")]
    public List<DownloadSnapshot> Snapshots { get; set; } = new();
}

public class AppCache
{
    [JsonPropertyName("repositories")]
    public Dictionary<string, RepositoryCache> Repositories { get; set; } = new();
}
