using System;
using System.Collections.Generic;
using System.Linq;
using GitHubDownloadCheck.Models;

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

public enum AnalyticsPeriod { Daily, Weekly, Monthly }

public class AnalyticsService
{
    public AnalyticsSummary Summarize(IReadOnlyList<GitHubRelease> releases)
    {
        if (releases.Count == 0)
            return new AnalyticsSummary(0, 0, "-", 0, "-", 0);

        var totalDownloads = releases.Sum(r => r.TotalDownloadCount);
        var bestRelease = releases.MaxBy(r => r.TotalDownloadCount)!;
        var allAssets = releases.SelectMany(r => r.Assets).ToList();
        var bestAsset = allAssets.Count > 0
            ? allAssets.MaxBy(a => a.DownloadCount)!
            : null;

        return new AnalyticsSummary(
            TotalDownloads: totalDownloads,
            ReleaseCount: releases.Count,
            MostPopularRelease: bestRelease.DisplayName,
            MostPopularReleaseCount: bestRelease.TotalDownloadCount,
            MostPopularAsset: bestAsset?.Name ?? "-",
            MostPopularAssetCount: bestAsset?.DownloadCount ?? 0
        );
    }

    public List<AssetBreakdown> GetAssetBreakdown(IReadOnlyList<GitHubRelease> releases)
    {
        return releases
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
        if (snapshots.Count < 2)
            return new List<PeriodDownloads>();

        // 連続するスナップショット間の差分を計算
        var deltas = new List<(DateTime At, long Delta)>();
        for (int i = 1; i < snapshots.Count; i++)
        {
            var prev = snapshots[i - 1].Releases.Sum(r => r.TotalDownloadCount);
            var curr = snapshots[i].Releases.Sum(r => r.TotalDownloadCount);
            var delta = Math.Max(0, curr - prev); // 負の値は無視
            deltas.Add((snapshots[i].FetchedAt.ToLocalTime(), delta));
        }

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
                    $"{g.Key:MM/dd}週"))
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

    private static DateTime GetWeekStart(DateTime dt)
    {
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return dt.Date.AddDays(-diff);
    }
}
