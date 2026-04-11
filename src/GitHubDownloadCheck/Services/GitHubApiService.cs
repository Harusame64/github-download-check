using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GitHubDownloadCheck.Models;
using GitHubDownloadCheck.Resources;

namespace GitHubDownloadCheck.Services;

public class GitHubApiService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GitHubApiService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubDownloadCheck", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<List<GitHubRelease>> GetReleasesAsync(string owner, string repo, CancellationToken ct = default)
    {
        var releases = new List<GitHubRelease>();
        var url = $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/releases?per_page=100";

        while (url is not null)
        {
            using var response = await _http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound    => new InvalidOperationException(string.Format(Strings.Api_RepoNotFound, owner, repo)),
                    System.Net.HttpStatusCode.Forbidden   => new InvalidOperationException(string.Format(Strings.Api_RateLimit, body)),
                    System.Net.HttpStatusCode.Unauthorized => new InvalidOperationException(Strings.Api_InvalidToken),
                    _ => new InvalidOperationException(string.Format(Strings.Api_GenericError, response.StatusCode, body))
                };
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var page = JsonSerializer.Deserialize<List<GitHubRelease>>(json, JsonOptions);
            if (page is not null)
                releases.AddRange(page);

            url = GetNextPageUrl(response);
        }

        return releases;
    }

    private static string? GetNextPageUrl(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var linkValues))
            return null;

        var link = string.Join(", ", linkValues);
        var match = Regex.Match(link, @"<([^>]+)>;\s*rel=""next""");
        return match.Success ? match.Groups[1].Value : null;
    }
}
