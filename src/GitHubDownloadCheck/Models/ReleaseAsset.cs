using System.Text.Json.Serialization;

namespace GitHubDownloadCheck.Models;

public class ReleaseAsset
{
    // GitHub のアセット ID。古いキャッシュには無いので 0 になる
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("download_count")]
    public long DownloadCount { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "";
}
