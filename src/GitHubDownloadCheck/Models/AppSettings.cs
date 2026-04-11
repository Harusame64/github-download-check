using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GitHubDownloadCheck.Models;

public class AppSettings
{
    [JsonPropertyName("gitHubToken")]
    public string GitHubToken { get; set; } = "";

    [JsonPropertyName("repositories")]
    public List<RepositoryEntry> Repositories { get; set; } = new();
}
