using System.Text.Json.Serialization;

namespace GitHubDownloadCheck.Models;

public class RepositoryEntry
{
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonIgnore]
    public string FullName => $"{Owner}/{Repo}";

    [JsonIgnore]
    public string Label => string.IsNullOrEmpty(DisplayName) ? FullName : DisplayName;
}
