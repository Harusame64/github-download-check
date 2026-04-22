using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitHubDownloadCheck.Models;
using GitHubDownloadCheck.Resources;
using GitHubDownloadCheck.Services;
using Avalonia;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace GitHubDownloadCheck.ViewModels;

file static class ChartPaints
{
    // テーマに応じたラベル色を返す（チャート構築のたびに評価）
    public static SolidColorPaint LabelPaint =>
        Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark
            ? new SolidColorPaint(new SKColor(200, 200, 200))  // ダーク: 明るいグレー
            : new SolidColorPaint(new SKColor(60, 60, 60));    // ライト: 暗いグレー

    public const float TextSize = 12f;
}

public partial class DashboardViewModel : ViewModelBase
{
    private readonly GitHubApiService _apiService;
    private readonly CacheService _cacheService;
    private readonly AnalyticsService _analyticsService;

    [ObservableProperty]
    private RepositoryEntry? _selectedRepository;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _hasData;

    // Summary
    [ObservableProperty] private long _totalDownloads;
    [ObservableProperty] private int _releaseCount;
    [ObservableProperty] private string _mostPopularRelease = "-";
    [ObservableProperty] private long _mostPopularReleaseCount;
    [ObservableProperty] private string _mostPopularAsset = "-";
    [ObservableProperty] private long _mostPopularAssetCount;

    // Charts
    [ObservableProperty] private double _downloadChartHeight = 300;
    [ObservableProperty] private double _assetChartHeight = 300;
    [ObservableProperty] private ISeries[] _downloadSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _assetSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _trendSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _releaseAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _assetAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _trendAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _downloadYAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _assetYAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _trendYAxes = Array.Empty<Axis>();

    // 期間別ダウンロード数チャート
    [ObservableProperty] private ISeries[] _periodSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _periodXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _periodYAxes = Array.Empty<Axis>();
    [ObservableProperty] private bool _hasPeriodData;

    [ObservableProperty] private bool _isPeriodDaily = true;
    [ObservableProperty] private bool _isPeriodWeekly;
    [ObservableProperty] private bool _isPeriodMonthly;

    // Data table
    public ObservableCollection<GitHubRelease> Releases { get; } = new();

    // Snapshot trend
    public ObservableCollection<SnapshotRow> SnapshotRows { get; } = new();

    private List<DownloadSnapshot> _currentSnapshots = new();
    private CancellationTokenSource? _cts;

    public DashboardViewModel(GitHubApiService apiService, CacheService cacheService, AnalyticsService analyticsService)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _analyticsService = analyticsService;
    }

    partial void OnSelectedRepositoryChanged(RepositoryEntry? value)
    {
        if (value is not null)
            _ = FetchAsync();
    }

    [RelayCommand]
    private async Task FetchAsync()
    {
        if (SelectedRepository is null) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsLoading = true;
        ErrorMessage = "";
        HasData = false;

        try
        {
            var releases = await _apiService.GetReleasesAsync(
                SelectedRepository.Owner, SelectedRepository.Repo, ct);

            _cacheService.SaveSnapshot(SelectedRepository.FullName, releases);
            ApplyData(releases);
            LoadSnapshotTrend(SelectedRepository.FullName);
            HasData = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyData(List<GitHubRelease> releases)
    {
        var summary = _analyticsService.Summarize(releases);
        TotalDownloads = summary.TotalDownloads;
        ReleaseCount = summary.ReleaseCount;
        MostPopularRelease = summary.MostPopularRelease;
        MostPopularReleaseCount = summary.MostPopularReleaseCount;
        MostPopularAsset = summary.MostPopularAsset;
        MostPopularAssetCount = summary.MostPopularAssetCount;

        // Releases list (newest first, excluding drafts)
        Releases.Clear();
        foreach (var r in releases.Where(r => !r.Draft).OrderByDescending(r => r.PublishedAt))
            Releases.Add(r);

        BuildDownloadChart(releases);
        BuildAssetChart(releases);
    }

    private void BuildDownloadChart(List<GitHubRelease> releases)
    {
        var ordered = releases.Where(r => !r.Draft).OrderBy(r => r.PublishedAt).TakeLast(20).ToList();
        int n = ordered.Count;
        var labels = ordered.Select(r => r.TagName).ToArray();
        var values = ordered.Select(r => (double)r.TotalDownloadCount).ToArray();
        DownloadChartHeight = Math.Max(200, n * 40 + 60);

        DownloadSeries = new ISeries[]
        {
            new RowSeries<double>
            {
                Values = values,
                Name = Strings.Chart_SeriesDownloads,
                Fill = new SolidColorPaint(SKColors.SteelBlue),
                DataLabelsFormatter = (point) =>
                {
                    int idx = point.Index;
                    return idx >= 0 && idx < values.Length ? $"{values[idx]:N0}" : string.Empty;
                },
                DataLabelsPaint = ChartPaints.LabelPaint,
                DataLabelsSize = ChartPaints.TextSize,
                DataLabelsPosition = DataLabelsPosition.End,
            }
        };

        // MinLimit=-0.5, MaxLimit=n-0.5 にすることでティックが各バーの中央 (0.5, 1.5, ...) に揃う
        ReleaseAxes = new[]
        {
            new Axis
            {
                Labeler = v =>
                {
                    // ティック位置: -0.5, 0.5, 1.5, ..., n-0.5
                    // バー中央: 0.5→labels[0], 1.5→labels[1], ...
                    int idx = (int)Math.Floor(v);
                    return idx >= 0 && idx < labels.Length ? labels[idx] : string.Empty;
                },
                LabelsPaint = ChartPaints.LabelPaint,
                TextSize = ChartPaints.TextSize,
                MinStep = 1,
                MinLimit = -0.5,
                MaxLimit = n - 0.5,
            }
        };

        DownloadYAxes = new[]
        {
            new Axis { Labeler = v => $"{v:N0}", TextSize = ChartPaints.TextSize, LabelsPaint = ChartPaints.LabelPaint }
        };
    }

    private void BuildAssetChart(List<GitHubRelease> releases)
    {
        // 降順取得後に反転: RowSeriesはindex0が下のため、最大値を上に表示するには昇順で渡す
        var breakdown = _analyticsService.GetAssetBreakdown(releases).Take(10).Reverse().ToList();
        if (breakdown.Count == 0) return;

        int n = breakdown.Count;
        var labels = breakdown.Select(a => a.AssetName).ToArray();
        var values = breakdown.Select(a => (double)a.TotalDownloads).ToArray();
        AssetChartHeight = Math.Max(200, n * 40 + 60);

        AssetSeries = new ISeries[]
        {
            new RowSeries<double>
            {
                Values = values,
                Name = Strings.Chart_SeriesAssets,
                Fill = new SolidColorPaint(SKColors.SeaGreen),
                DataLabelsFormatter = (point) =>
                {
                    int idx = point.Index;
                    return idx >= 0 && idx < values.Length ? $"{values[idx]:N0}" : string.Empty;
                },
                DataLabelsPaint = ChartPaints.LabelPaint,
                DataLabelsSize = ChartPaints.TextSize,
                DataLabelsPosition = DataLabelsPosition.End,
            }
        };

        AssetAxes = new[]
        {
            new Axis
            {
                Labeler = v =>
                {
                    int idx = (int)Math.Floor(v);
                    return idx >= 0 && idx < labels.Length ? labels[idx] : string.Empty;
                },
                LabelsPaint = ChartPaints.LabelPaint,
                TextSize = ChartPaints.TextSize,
                MinStep = 1,
                MinLimit = -0.5,
                MaxLimit = n - 0.5,
            }
        };

        AssetYAxes = new[]
        {
            new Axis { Labeler = v => $"{v:N0}", TextSize = ChartPaints.TextSize, LabelsPaint = ChartPaints.LabelPaint }
        };
    }

    [RelayCommand]
    private void SetPeriodDaily()  { IsPeriodDaily = true;  IsPeriodWeekly = false; IsPeriodMonthly = false; BuildPeriodChart(); }
    [RelayCommand]
    private void SetPeriodWeekly() { IsPeriodDaily = false; IsPeriodWeekly = true;  IsPeriodMonthly = false; BuildPeriodChart(); }
    [RelayCommand]
    private void SetPeriodMonthly(){ IsPeriodDaily = false; IsPeriodWeekly = false; IsPeriodMonthly = true;  BuildPeriodChart(); }

    private void BuildPeriodChart()
    {
        var period = IsPeriodWeekly ? AnalyticsPeriod.Weekly
                   : IsPeriodMonthly ? AnalyticsPeriod.Monthly
                   : AnalyticsPeriod.Daily;

        var data = _analyticsService.GetPeriodDownloads(_currentSnapshots, period);
        HasPeriodData = data.Count > 0;
        if (!HasPeriodData) return;

        var labels = data.Select(d => d.Label).ToArray();
        var values = data.Select(d => (double)d.Downloads).ToArray();

        SKColor color = period switch
        {
            AnalyticsPeriod.Weekly  => SKColors.MediumPurple,
            AnalyticsPeriod.Monthly => SKColors.Coral,
            _                       => SKColors.CornflowerBlue,
        };

        PeriodSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = values,
                Name = period switch
                {
                    AnalyticsPeriod.Weekly  => Strings.Chart_SeriesWeekly,
                    AnalyticsPeriod.Monthly => Strings.Chart_SeriesMonthly,
                    _                       => Strings.Chart_SeriesDaily,
                },
                Fill = new SolidColorPaint(color),
            }
        };

        PeriodXAxes = new[] { new Axis { Labels = labels, LabelsRotation = -35, TextSize = ChartPaints.TextSize, MinStep = 1, LabelsPaint = ChartPaints.LabelPaint } };
        PeriodYAxes = new[] { new Axis { Labeler = v => $"{v:N0}", TextSize = ChartPaints.TextSize, LabelsPaint = ChartPaints.LabelPaint } };
    }

    private void LoadSnapshotTrend(string repoKey)
    {
        var rawSnapshots = _cacheService.GetSnapshots(repoKey);
        
        // 同一日に複数回収集した場合は、その日の最終分のみを採用して集約する
        var snapshots = rawSnapshots
            .GroupBy(s => s.FetchedAt.ToLocalTime().Date)
            .Select(g => g.OrderByDescending(s => s.FetchedAt).First())
            .OrderBy(s => s.FetchedAt)
            .ToList();

        _currentSnapshots = snapshots;
        SnapshotRows.Clear();

        for (int i = 0; i < snapshots.Count; i++)
        {
            var snap = snapshots[i];
            var total = snap.Releases.Sum(r => r.TotalDownloadCount);
            long? delta = i > 0
                ? total - snapshots[i - 1].Releases.Sum(r => r.TotalDownloadCount)
                : null;

            SnapshotRows.Add(new SnapshotRow(snap.FetchedAt.ToLocalTime(), total, delta));
        }

        if (snapshots.Count >= 2)
        {
            var trendValues = snapshots
                .Select(s => new DateTimePoint(s.FetchedAt, (double)s.Releases.Sum(r => r.TotalDownloadCount)))
                .ToArray();

            TrendSeries = new ISeries[]
            {
                new LineSeries<DateTimePoint>
                {
                    Values = trendValues,
                    Name = Strings.Chart_SeriesTrend,
                    Stroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                    Fill = null,
                    GeometrySize = 8,

                }
            };

            TrendAxes = new[]
            {
                new Axis
                {
                    Labeler = value =>
                    {
                        var ticks = (long)value;
                        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return "";
                        return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("MM/dd");
                    },
                    LabelsRotation = -35,
                    TextSize = ChartPaints.TextSize,
                    UnitWidth = TimeSpan.FromDays(1).Ticks,
                    MinStep = TimeSpan.FromHours(1).Ticks,
                    LabelsPaint = ChartPaints.LabelPaint,
                }
            };

            TrendYAxes = new[]
            {
                new Axis { Labeler = v => $"{v:N0}", TextSize = ChartPaints.TextSize, LabelsPaint = ChartPaints.LabelPaint }
            };
        }

        // 期間別ダウンロード数チャート
        BuildPeriodChart();
    }
}

public class SnapshotRow(DateTime fetchedAt, long totalDownloads, long? delta)
{
    public string FetchedAtText { get; } = fetchedAt.ToString("yyyy/MM/dd HH:mm");
    public string TotalDownloadsText { get; } = totalDownloads.ToString("N0");
    public string DeltaText { get; } = delta.HasValue ? $"+{delta.Value:N0}" : "-";
}
