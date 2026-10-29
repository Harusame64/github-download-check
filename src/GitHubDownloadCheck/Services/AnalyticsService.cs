using System;
using System.Collections.Generic;
using System.Linq;
using GitHubDownloadCheck.Models;
using GitHubDownloadCheck.Resources;

namespace GitHubDownloadCheck.Services;

public record AnalyticsSummary(
    long TotalDownloads,
    int ReleaseCount,
    string MostPopularRelease,
    long MostPopularReleaseCount,
    string MostPopularAsset,
    long MostPopularAssetCount
);

public record AssetBreakdown(string AssetName, long TotalDownloads);

public record PeriodDownloads(DateTime PeriodStart, long Downloads, string Label);

/// <summary>スナップショット1件ぶんの累計と、前回からの増分（初回は null）。</summary>
public record SnapshotPoint(DateTime FetchedAt, long Total, long? Delta);

public enum AnalyticsPeriod { Daily, Weekly, Monthly }

public class AnalyticsService
{
    public AnalyticsSummary Summarize(IReadOnlyList<GitHubRelease> releases)
    {
        var nonDraft = releases.Where(r => !r.Draft).ToList();
        if (nonDraft.Count == 0)
            return new AnalyticsSummary(0, 0, "-", 0, "-", 0);

        var totalDownloads = nonDraft.Sum(r => r.TotalDownloadCount);
        var bestRelease = nonDraft.MaxBy(r => r.TotalDownloadCount)!;
        var bestAsset = nonDraft
            .SelectMany(r => r.Assets)
            .GroupBy(a => a.Name)
            .Select(g => new AssetBreakdown(g.Key, g.Sum(a => a.DownloadCount)))
            .MaxBy(a => a.TotalDownloads);

        return new AnalyticsSummary(
            TotalDownloads: totalDownloads,
            ReleaseCount: nonDraft.Count,
            MostPopularRelease: bestRelease.DisplayName,
            MostPopularReleaseCount: bestRelease.TotalDownloadCount,
            MostPopularAsset: bestAsset?.AssetName ?? "-",
            MostPopularAssetCount: bestAsset?.TotalDownloads ?? 0
        );
    }

    public List<AssetBreakdown> GetAssetBreakdown(IReadOnlyList<GitHubRelease> releases)
    {
        return releases
            .Where(r => !r.Draft)
            .SelectMany(r => r.Assets)
            .GroupBy(a => a.Name)
            .Select(g => new AssetBreakdown(g.Key, g.Sum(a => a.DownloadCount)))
            .OrderByDescending(a => a.TotalDownloads)
            .ToList();
    }

    /// <summary>
    /// スナップショット差分から日別・週別・月別のダウンロード数を集計する。
    /// </summary>
    public List<PeriodDownloads> GetPeriodDownloads(
        IReadOnlyList<DownloadSnapshot> snapshots,
        AnalyticsPeriod period)
    {
        var points = GetSnapshotPoints(snapshots);
        if (points.Count < 2)
            return new List<PeriodDownloads>();

        var deltas = points.Skip(1).Select(p => (At: p.FetchedAt.ToLocalTime(), Delta: p.Delta ?? 0)).ToList();

        return period switch
        {
            AnalyticsPeriod.Daily => deltas
                .GroupBy(d => d.At.Date)
                .Select(g => new PeriodDownloads(
                    g.Key,
                    g.Sum(x => x.Delta),
                    g.Key.ToString("MM/dd")))
                .OrderBy(x => x.PeriodStart)
                .ToList(),

            AnalyticsPeriod.Weekly => deltas
                .GroupBy(d => GetWeekStart(d.At))
                .Select(g => new PeriodDownloads(
                    g.Key,
                    g.Sum(x => x.Delta),
                    $"{g.Key:MM/dd}{Strings.Analytics_WeekSuffix}"))
                .OrderBy(x => x.PeriodStart)
                .ToList(),

            AnalyticsPeriod.Monthly => deltas
                .GroupBy(d => new DateTime(d.At.Year, d.At.Month, 1))
                .Select(g => new PeriodDownloads(
                    g.Key,
                    g.Sum(x => x.Delta),
                    g.Key.ToString("yyyy/MM")))
                .OrderBy(x => x.PeriodStart)
                .ToList(),

            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
    }

    /// <summary>
    /// スナップショットごとの累計と増分を求める。
    /// 増分はアセット単位で「前回わかっていた値からの増加」だけを数えるので、負にならない。
    /// 取得が不完全だった回（リリースやアセットが欠けた回）があっても、
    /// 欠けた分は減少とみなさず、次に現れたときも二重には数えない。
    /// </summary>
    public List<SnapshotPoint> GetSnapshotPoints(IReadOnlyList<DownloadSnapshot> snapshots)
    {
        var result = new List<SnapshotPoint>();
        // アセットは GitHub のアセット ID で識別する（名前の変更や同名の再アップロードに強い）。
        // ID を保存していなかった頃のキャッシュは名前で識別し、ID 付きの回に切り替わるときに一度だけ引き継ぐ
        var lastKnownById = new Dictionary<long, long>();
        var lastKnownByName = new Dictionary<string, long>();
        long cumulative = 0;

        foreach (var snap in snapshots)
        {
            long delta = 0;
            foreach (var r in snap.Releases)
            {
                foreach (var a in r.Assets)
                {
                    var nameKey = $"{r.Id}/{r.TagName}/{a.Name}";
                    long prev;
                    if (a.Id == 0)
                    {
                        lastKnownByName.TryGetValue(nameKey, out prev);
                    }
                    else if (!lastKnownById.TryGetValue(a.Id, out prev)
                             && lastKnownByName.Remove(nameKey, out var legacy))
                    {
                        prev = legacy;
                    }

                    if (a.DownloadCount > prev)
                        delta += a.DownloadCount - prev;

                    var known = Math.Max(prev, a.DownloadCount);
                    if (a.Id == 0)
                        lastKnownByName[nameKey] = known;
                    else
                        lastKnownById[a.Id] = known;
                }
            }

            cumulative += delta;
            result.Add(new SnapshotPoint(snap.FetchedAt, cumulative, result.Count == 0 ? null : delta));
        }

        return result;
    }

    private static DateTime GetWeekStart(DateTime dt)
    {
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return dt.Date.AddDays(-diff);
    }
}
