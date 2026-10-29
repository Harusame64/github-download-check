using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GitHubDownloadCheck.Models;

public class AppSettings
{
    [JsonPropertyName("gitHubToken")]
    public string GitHubToken { get; set; } = "";

    /// <summary>表示言語。"auto"（OS に合わせる）/ "ja" / "en"</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "auto";

    [JsonPropertyName("repositories")]
    public List<RepositoryEntry> Repositories { get; set; } = new();
}
