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
    private static bool IsDark => Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

    // どのチャートも系列は1つなので、同じ青で揃える（ダーク用は暗い背景向けに一段明るい青）
    public static SKColor SeriesColor => IsDark ? new SKColor(0x39, 0x87, 0xE5) : new SKColor(0x2A, 0x78, 0xD6);

    // テーマに応じたラベル色を返す（チャート構築のたびに評価）
    public static SolidColorPaint LabelPaint => new(IsDark ? new SKColor(0xC3, 0xC2, 0xB7) : new SKColor(0x52, 0x51, 0x4E));

    // 目盛線は控えめに
    public static SolidColorPaint GridPaint => new(IsDark ? new SKColor(255, 255, 255, 22) : new SKColor(0, 0, 0, 18), 1);

    public static SolidColorPaint SeriesFill => new(SeriesColor);

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
    private string _statusText = "";

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
    [ObservableProperty] private bool _hasAssetData;
    [ObservableProperty] private bool _hasTrendData;

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

    /// <summary>チャートの中身を差し替えたときに発火する。View 側で再描画を確実に行うために使う。</summary>
    public event EventHandler? ChartsRebuilt;

    private List<GitHubRelease> _currentReleases = new();
    private List<DownloadSnapshot> _currentSnapshots = new();
    private CancellationTokenSource? _cts;
    // 選択が変わるたびに進める。古い取得の結果が後から届いても、これが違えば捨てる
    private int _loadGeneration;
    private Func<string>? _statusFactory;

    public DashboardViewModel(GitHubApiService apiService, CacheService cacheService, AnalyticsService analyticsService)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _analyticsService = analyticsService;
    }

    partial void OnSelectedRepositoryChanged(RepositoryEntry? value)
    {
        if (value is not null)
        {
            _ = LoadAsync(value, showCachedFirst: true);
            return;
        }

        // 選択が外れた（選択中のリポジトリが削除された等）。取得中のものは捨てて画面を空にする
        ++_loadGeneration;
        _cts?.Cancel();
        IsLoading = false;
        ErrorMessage = "";
        Clear();
        SetStatus(() => "");
    }

    [RelayCommand]
    private Task FetchAsync() =>
        SelectedRepository is null ? Task.CompletedTask : LoadAsync(SelectedRepository, showCachedFirst: false);

    private async Task LoadAsync(RepositoryEntry repo, bool showCachedFirst)
    {
        // リポジトリは引数で固定する。await の後に SelectedRepository を読むと、
        // その間に切り替わった別リポジトリの名前で保存・表示してしまう
        var generation = ++_loadGeneration;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        bool IsCurrent() => generation == _loadGeneration;

        IsLoading = true;
        ErrorMessage = "";

        try
        {
            if (showCachedFirst)
            {
                // 通信を待たずに、前回の取得結果をすぐ出す
                var cached = await _cacheService.GetSnapshotsAsync(repo.FullName);
                if (!IsCurrent()) return;

                if (cached.Count > 0)
                {
                    Apply(cached[^1].Releases, cached);
                    SetStatus(() => Strings.Dashboard_Refreshing);
                }
                else
                {
                    Clear();
                    SetStatus(() => Strings.Dashboard_Loading);
                }
            }
            else
            {
                SetStatus(() => Strings.Dashboard_Refreshing);
            }

            var releases = await _apiService.GetReleasesAsync(repo.Owner, repo.Repo, ct);
            var snapshots = await _cacheService.SaveSnapshotAsync(repo.FullName, releases);
            if (!IsCurrent()) return;

            Apply(releases, snapshots);
            var fetchedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            SetStatus(() => string.Format(Strings.Dashboard_LastUpdated, fetchedAt));
        }
        // 自分で取り消したときだけ黙る。HttpClient のタイムアウトも同じ例外型なので、それは失敗として扱う
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!IsCurrent()) return;

            if (HasData && _currentSnapshots.Count > 0)
            {
                var at = _currentSnapshots[^1].FetchedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
                ErrorMessage = string.Format(Strings.Dashboard_ShowingCached, at) + "\n" + ex.Message;
                SetStatus(() => string.Format(Strings.Dashboard_LastUpdated, at));
            }
            else
            {
                ErrorMessage = ex.Message;
                SetStatus(() => "");
            }
        }
        finally
        {
            if (IsCurrent())
                IsLoading = false;
        }
    }

    // 状態表示は「作り方」ごと持っておき、言語を切り替えたら同じ作り方で作り直す
    private void SetStatus(Func<string> factory)
    {
        _statusFactory = factory;
        StatusText = factory();
    }

    /// <summary>表示言語を切り替えたあと、ViewModel が持っている文言を作り直す。</summary>
    public void RefreshLocalizedText()
    {
        if (_statusFactory is not null)
            StatusText = _statusFactory();
        BuildCharts();
    }

    private void Apply(List<GitHubRelease> releases, List<DownloadSnapshot> snapshots)
    {
        _currentReleases = releases;
        _currentSnapshots = snapshots;

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

        LoadSnapshotRows();
        BuildCharts();
        HasData = true;
    }

    private void Clear()
    {
        _currentReleases = new();
        _currentSnapshots = new();
        TotalDownloads = 0;
        ReleaseCount = 0;
        MostPopularRelease = "-";
        MostPopularReleaseCount = 0;
        MostPopularAsset = "-";
        MostPopularAssetCount = 0;
        Releases.Clear();
        SnapshotRows.Clear();
        BuildCharts();
        HasData = false;
    }

    /// <summary>手元のデータからチャートを作り直す（テーマ切替時にも呼ぶ）。</summary>
    public void BuildCharts()
    {
        BuildDownloadChart(_currentReleases);
        BuildAssetChart(_currentReleases);
        BuildTrendChart();
        BuildPeriodChart();
        ChartsRebuilt?.Invoke(this, EventArgs.Empty);
    }

    // 横棒グラフではバー末尾に値を出しているので、縦の目盛線は引かない
    private static Axis ValueAxis(double maxValue, bool showGrid = true) => new()
    {
        Labeler = v => $"{v:N0}",
        TextSize = ChartPaints.TextSize,
        // 横棒グラフは値をバー末尾に出しているので、目盛ラベルも出さない（幅が広いと目盛が詰まって読めない）
        LabelsPaint = showGrid ? ChartPaints.LabelPaint : null,
        SeparatorsPaint = showGrid ? ChartPaints.GridPaint : null,
        MinLimit = 0,
        // DL数は整数なので、値が小さいときに 0,0,1,1… と同じ目盛が並ばないようにする
        MinStep = 1,
        // 値がすべて0でも軸が潰れないように。横棒グラフは最長バーの末尾の値が切れないよう右に余白を取る
        // （値ラベルの幅は桁数で決まるので、余白も桁数に比例させる。6桁で約1.24倍、9桁で約1.36倍）
        MaxLimit = maxValue <= 0 ? 1 : showGrid ? null : maxValue * (1 + 0.04 * $"{maxValue:N0}".Length),
    };

    // 名前が長すぎると左の軸が幅を取り、バーの描ける幅が狭くなるので省略する
    private const int MaxCategoryLabelLength = 32;

    private static string ShortenLabel(string label) =>
        label.Length <= MaxCategoryLabelLength ? label : label[..(MaxCategoryLabelLength - 1)] + "…";

    private static Axis CategoryAxis(string[] labels) => new()
    {
        // MinLimit=-0.5, MaxLimit=n-0.5 にすることでティックが各バーの中央 (0.5, 1.5, ...) に揃う
        Labeler = v =>
        {
            // ティック位置: -0.5, 0.5, 1.5, ..., n-0.5
            // バー中央: 0.5→labels[0], 1.5→labels[1], ...
            int idx = (int)Math.Floor(v);
            return idx >= 0 && idx < labels.Length ? ShortenLabel(labels[idx]) : string.Empty;
        },
        LabelsPaint = ChartPaints.LabelPaint,
        SeparatorsPaint = null,
        TextSize = ChartPaints.TextSize,
        MinStep = 1,
        MinLimit = -0.5,
        MaxLimit = Math.Max(labels.Length, 1) - 0.5,
    };

    private static RowSeries<double> HorizontalBars(double[] values, string name) => new()
    {
        Values = values,
        Name = name,
        Fill = ChartPaints.SeriesFill,
        Rx = 4,
        Ry = 4,
        MaxBarWidth = 22,
        DataLabelsFormatter = point =>
        {
            int idx = point.Index;
            return idx >= 0 && idx < values.Length ? $"{values[idx]:N0}" : string.Empty;
        },
        DataLabelsPaint = ChartPaints.LabelPaint,
        DataLabelsSize = ChartPaints.TextSize,
        DataLabelsPosition = DataLabelsPosition.End,
    };

    private void BuildDownloadChart(List<GitHubRelease> releases)
    {
        var ordered = releases.Where(r => !r.Draft).OrderBy(r => r.PublishedAt).TakeLast(20).ToList();
        int n = ordered.Count;
        var labels = ordered.Select(r => r.TagName).ToArray();
        var values = ordered.Select(r => (double)r.TotalDownloadCount).ToArray();
        DownloadChartHeight = Math.Max(160, n * 40 + 60);

        DownloadSeries = n == 0 ? Array.Empty<ISeries>() : new ISeries[] { HorizontalBars(values, Strings.Chart_SeriesDownloads) };
        ReleaseAxes = new[] { CategoryAxis(labels) };
        DownloadYAxes = new[] { ValueAxis(values.DefaultIfEmpty(0).Max(), showGrid: false) };
    }

    private void BuildAssetChart(List<GitHubRelease> releases)
    {
        // 降順取得後に反転: RowSeriesはindex0が下のため、最大値を上に表示するには昇順で渡す
        var breakdown = _analyticsService.GetAssetBreakdown(releases).Take(10).Reverse().ToList();
        int n = breakdown.Count;
        var labels = breakdown.Select(a => a.AssetName).ToArray();
        var values = breakdown.Select(a => (double)a.TotalDownloads).ToArray();
        AssetChartHeight = Math.Max(160, n * 40 + 60);

        // 前のリポジトリのチャートが残らないよう、空のときも必ず差し替える
        HasAssetData = n > 0;
        AssetSeries = n == 0 ? Array.Empty<ISeries>() : new ISeries[] { HorizontalBars(values, Strings.Chart_SeriesAssets) };
        AssetAxes = new[] { CategoryAxis(labels) };
        AssetYAxes = new[] { ValueAxis(values.DefaultIfEmpty(0).Max(), showGrid: false) };
    }

    [RelayCommand]
    private void SetPeriodDaily()  { IsPeriodDaily = true;  IsPeriodWeekly = false; IsPeriodMonthly = false; BuildPeriodChart(); ChartsRebuilt?.Invoke(this, EventArgs.Empty); }
    [RelayCommand]
    private void SetPeriodWeekly() { IsPeriodDaily = false; IsPeriodWeekly = true;  IsPeriodMonthly = false; BuildPeriodChart(); ChartsRebuilt?.Invoke(this, EventArgs.Empty); }
    [RelayCommand]
    private void SetPeriodMonthly(){ IsPeriodDaily = false; IsPeriodWeekly = false; IsPeriodMonthly = true;  BuildPeriodChart(); ChartsRebuilt?.Invoke(this, EventArgs.Empty); }

    private void BuildPeriodChart()
    {
        var period = IsPeriodWeekly ? AnalyticsPeriod.Weekly
                   : IsPeriodMonthly ? AnalyticsPeriod.Monthly
                   : AnalyticsPeriod.Daily;

        var data = _analyticsService.GetPeriodDownloads(_currentSnapshots, period);
        HasPeriodData = data.Count > 0;

        var labels = data.Select(d => d.Label).ToArray();
        var values = data.Select(d => (double)d.Downloads).ToArray();

        PeriodSeries = !HasPeriodData ? Array.Empty<ISeries>() : new ISeries[]
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
                Fill = ChartPaints.SeriesFill,
                Rx = 4,
                Ry = 4,
                MaxBarWidth = 28,
            }
        };

        PeriodXAxes = new[] { new Axis { Labels = labels, LabelsRotation = labels.Length > 12 ? -35 : 0, SeparatorsPaint = null, TextSize = ChartPaints.TextSize, MinStep = 1, LabelsPaint = ChartPaints.LabelPaint } };
        PeriodYAxes = new[] { ValueAxis(values.DefaultIfEmpty(0).Max()) };
    }

    private void LoadSnapshotRows()
    {
        // 増分はアセット単位で数えるので、取得に失敗した回が混じっても負にならない
        var points = _analyticsService.GetSnapshotPoints(_currentSnapshots);

        SnapshotRows.Clear();
        // 最新のものを上に表示する
        for (int i = points.Count - 1; i >= 0; i--)
            SnapshotRows.Add(new SnapshotRow(points[i].FetchedAt.ToLocalTime(), points[i].Total, points[i].Delta));
    }

    private void BuildTrendChart()
    {
        var points = _analyticsService.GetSnapshotPoints(_currentSnapshots);
        HasTrendData = points.Count >= 2;
        if (!HasTrendData)
        {
            TrendSeries = Array.Empty<ISeries>();
            TrendAxes = Array.Empty<Axis>();
            TrendYAxes = Array.Empty<Axis>();
            return;
        }

        var trendValues = points.Select(p => new DateTimePoint(p.FetchedAt, p.Total)).ToArray();

        TrendSeries = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = trendValues,
                Name = Strings.Chart_SeriesTrend,
                Stroke = new SolidColorPaint(ChartPaints.SeriesColor, 2),
                Fill = new SolidColorPaint(ChartPaints.SeriesColor.WithAlpha(28)),
                // 点が多いと丸が重なって見づらいので、少ないときだけ打つ
                GeometrySize = points.Count > 30 ? 0 : 7,
                GeometryStroke = new SolidColorPaint(ChartPaints.SeriesColor, 2),
                LineSmoothness = 0,
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
                TextSize = ChartPaints.TextSize,
                SeparatorsPaint = null,
                UnitWidth = TimeSpan.FromDays(1).Ticks,
                MinStep = TimeSpan.FromDays(1).Ticks,
                LabelsPaint = ChartPaints.LabelPaint,
            }
        };

        var min = points.Min(p => p.Total);
        var max = points.Max(p => p.Total);
        TrendYAxes = new[]
        {
            new Axis
            {
                Labeler = v => $"{v:N0}",
                TextSize = ChartPaints.TextSize,
                LabelsPaint = ChartPaints.LabelPaint,
                SeparatorsPaint = ChartPaints.GridPaint,
                MinStep = 1,
                MaxLimit = min == max ? max + 1 : null,
                MinLimit = min == max ? Math.Max(0, min - 1) : null,
            }
        };
    }
}

public class SnapshotRow(DateTime fetchedAt, long totalDownloads, long? delta)
{
    public string FetchedAtText { get; } = fetchedAt.ToString("yyyy/MM/dd HH:mm");
    public string TotalDownloadsText { get; } = totalDownloads.ToString("N0");
    public string DeltaText { get; } = delta.HasValue ? $"+{delta.Value:N0}" : "-";
}
