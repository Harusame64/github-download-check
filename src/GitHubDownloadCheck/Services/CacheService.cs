using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GitHubDownloadCheck.Models;

namespace GitHubDownloadCheck.Services;

public class CacheService
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitHubDownloadCheck");

    private static readonly string CachePath = Path.Combine(AppDataDir, "cache.json");

    // 1日1件に集約して保持するので、これで約1年分
    private const int MaxSnapshots = 400;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ファイルは起動後に一度だけ読み、以降はメモリ上のものを使う（全I/Oはこのロックの内側）
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppCache? _cache;

    private AppCache LoadFromDisk()
    {
        if (!File.Exists(CachePath))
            return new AppCache();

        try
        {
            using var stream = File.OpenRead(CachePath);
            var cache = JsonSerializer.Deserialize<AppCache>(stream, JsonOptions) ?? new AppCache();
            foreach (var repo in cache.Repositories.Values)
                Compact(repo);
            return cache;
        }
        catch
        {
            // 壊れたファイルを次の保存で上書きして履歴を失わないよう、退避してから空で始める
            try { File.Move(CachePath, CachePath + ".bad", overwrite: true); } catch { }
            return new AppCache();
        }
    }

    private async Task<AppCache> GetCacheAsync()
    {
        _cache ??= await Task.Run(LoadFromDisk);
        return _cache;
    }

    /// <summary>
    /// スナップショットを追加し、追加後のそのリポジトリの全スナップショット（古い順）を返す。
    /// 空の結果（全リリース削除など）もそのまま保存する。増分はアセット単位で数えるので、
    /// アセットが消えた回があっても負にはならない。
    /// </summary>
    public async Task<List<DownloadSnapshot>> SaveSnapshotAsync(string repoKey, List<GitHubRelease> releases)
    {
        await _lock.WaitAsync();
        try
        {
            var cache = await GetCacheAsync();
            if (!cache.Repositories.TryGetValue(repoKey, out var repoCache))
            {
                repoCache = new RepositoryCache();
                cache.Repositories[repoKey] = repoCache;
            }

            repoCache.Snapshots.Add(new DownloadSnapshot
            {
                FetchedAt = DateTime.UtcNow,
                Releases = releases.Select(StripForCache).ToList(),
            });
            Compact(repoCache);

            // 書き込みに失敗しても（ウイルス対策のロック・容量不足など）取得結果は表示できるように握りつぶす。
            // メモリ上には残っているので、次に保存できたときにまとめて書かれる
            try
            {
                await Task.Run(() => WriteAtomic(JsonSerializer.SerializeToUtf8Bytes(cache, JsonOptions)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            return repoCache.Snapshots.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<DownloadSnapshot>> GetSnapshotsAsync(string repoKey)
    {
        await _lock.WaitAsync();
        try
        {
            var cache = await GetCacheAsync();
            return cache.Repositories.TryGetValue(repoKey, out var repoCache)
                ? repoCache.Snapshots.ToList()
                : new List<DownloadSnapshot>();
        }
        finally
        {
            _lock.Release();
        }
    }

    // 同じ日（ローカル時刻）のスナップショットは最後の1件だけ残す。表示も日単位なので失うものはない。
    // ただし、その日のうちに消えたアセットを含む回は残す（捨てるとそのアセットの最後の値がわからなくなる）
    private static void Compact(RepositoryCache repoCache)
    {
        var compacted = repoCache.Snapshots
            .GroupBy(s => s.FetchedAt.ToLocalTime().Date)
            .SelectMany(g =>
            {
                var latest = g.MaxBy(s => s.FetchedAt)!;
                var latestKeys = AssetKeys(latest).ToHashSet();
                return g.Where(s => s == latest || AssetKeys(s).Any(k => !latestKeys.Contains(k)));
            })
            .OrderBy(s => s.FetchedAt)
            .TakeLast(MaxSnapshots)
            .ToList();

        foreach (var snap in compacted)
            snap.Releases = snap.Releases.Select(StripForCache).ToList();

        repoCache.Snapshots = compacted;
    }

    // アセットを名前で識別する（ID を持たない古いキャッシュとも比べられるように、ID ではなく名前で揃える）
    private static IEnumerable<string> AssetKeys(DownloadSnapshot s) =>
        s.Releases.SelectMany(r => r.Assets.Select(a => $"{r.Id}/{r.TagName}/{a.Name}"));

    // リリースノート本文は集計に使わず、キャッシュを大きく膨らませるだけなので保存しない
    private static GitHubRelease StripForCache(GitHubRelease r) => r.Body is null ? r : new GitHubRelease
    {
        Id = r.Id,
        TagName = r.TagName,
        Name = r.Name,
        PublishedAt = r.PublishedAt,
        Prerelease = r.Prerelease,
        Draft = r.Draft,
        Assets = r.Assets,
    };

    private static void WriteAtomic(byte[] json)
    {
        Directory.CreateDirectory(AppDataDir);
        var tmp = CachePath + ".tmp";
        File.WriteAllBytes(tmp, json);
        File.Move(tmp, CachePath, overwrite: true);
    }
}
