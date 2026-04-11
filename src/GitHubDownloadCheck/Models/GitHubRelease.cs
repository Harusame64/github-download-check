using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GitHubDownloadCheck.Models;

public class GitHubRelease
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<ReleaseAsset> Assets { get; set; } = new();

    [JsonIgnore]
    public long TotalDownloadCount => Assets.Sum(a => a.DownloadCount);

    [JsonIgnore]
    public string DisplayName => string.IsNullOrEmpty(Name) ? TagName : Name;

    [JsonIgnore]
    public string PublishedAtText => PublishedAt?.ToLocalTime().ToString("yyyy/MM/dd") ?? "";

    [JsonIgnore]
    public string TotalDownloadCountText => TotalDownloadCount.ToString("N0");
}
