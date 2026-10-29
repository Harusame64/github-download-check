using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using GitHubDownloadCheck.ViewModels;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Avalonia;

namespace GitHubDownloadCheck.Views;

public partial class DashboardView : UserControl
{
    private readonly CartesianChart[] _charts;
    // 中身を差し替えたのに、まだ測り直し（Measure）が通っていないチャート
    private readonly HashSet<CartesianChart> _pending = new();
    private readonly DispatcherTimer _retryTimer;
    private DashboardViewModel? _vm;

    public DashboardView()
    {
        InitializeComponent();
        _charts = new[] { TrendChart, PeriodChart, ReleaseChart, AssetChart };

        // LiveCharts は、前回の Measure のあとに1フレームも描いていないと、次の Measure を黙って飛ばし、
        // 飛ばした分をやり直さない（Chart.IsRendering）。ScrollViewer の外にいるチャートはフレームを描かないので、
        // 画面外で中身を2回差し替える（キャッシュ表示→取得結果、リポジトリの連続切替）と後の方が捨てられ、
        // 前のリポジトリのグラフが新しい高さの枠に描かれて、見切れたり余白が残ったりしていた。
        // そこで「Measure が実際に走った」ことを Measuring で確かめ、走るまで画面に見えている間は頼み直す
        foreach (var chart in _charts)
        {
            var c = chart;
            c.CoreChart.Measuring += _ => _pending.Remove(c);
            // 高さが変わったときの LiveCharts 自身の測り直しも同じ理由で捨てられうる
            c.SizeChanged += (_, _) => MarkPending(c);
        }

        _retryTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => RetryPending());

        // スクロール時にチャートの描画を強制的に更新（高速スクロール時の描画漏れ対策）。
        // 画面外で取り残されたチャートは、見えてきたここで測り直す
        MainScrollViewer.ScrollChanged += (_, _) =>
        {
            foreach (var chart in _charts)
                chart.InvalidateVisual();
            RetryPending();
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm is not null)
        {
            _vm.ChartsRebuilt -= OnChartsRebuilt;
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as DashboardViewModel;

        if (_vm is not null)
        {
            _vm.ChartsRebuilt += OnChartsRebuilt;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // カードの表示/非表示が切り替わったときも描き直す
        if (e.PropertyName is nameof(DashboardViewModel.HasData)
            or nameof(DashboardViewModel.HasTrendData)
            or nameof(DashboardViewModel.HasPeriodData)
            or nameof(DashboardViewModel.HasAssetData))
            MarkAllPending();
    }

    private void OnChartsRebuilt(object? sender, EventArgs e) => MarkAllPending();

    private void MarkAllPending()
    {
        foreach (var chart in _charts)
            MarkPending(chart);
    }

    private void MarkPending(CartesianChart chart)
    {
        _pending.Add(chart);
        // レイアウト確定後に頼む（その前だと古い大きさで測ってしまう）
        if (!_retryTimer.IsEnabled)
            _retryTimer.Start();
    }

    // 測り直しが済んでいないチャートのうち、いま画面に見えているものに頼み直す。
    // 見えていないものは頼んでも捨てられるので、スクロールで見えてきたときに頼む
    private void RetryPending()
    {
        var anyVisible = false;
        foreach (var chart in new List<CartesianChart>(_pending))
        {
            if (!IsOnScreen(chart)) continue;
            anyVisible = true;
            // フレームを1枚描かせてから測り直す（描いていないと Measure がまた飛ばされる）
            chart.InvalidateVisual();
            chart.CoreChart.Update(new ChartUpdateParams { IsAutomaticUpdate = false, Throttling = false });
        }

        if (!anyVisible)
            _retryTimer.Stop();
        else if (!_retryTimer.IsEnabled)
            _retryTimer.Start();
    }

    private bool IsOnScreen(CartesianChart chart)
    {
        if (!chart.IsEffectivelyVisible || chart.Bounds.Width <= 0 || chart.Bounds.Height <= 0)
            return false;
        if (chart.TransformToVisual(MainScrollViewer) is not { } m)
            return false;
        var rect = new Rect(chart.Bounds.Size).TransformToAABB(m);
        return rect.Intersects(new Rect(MainScrollViewer.Bounds.Size));
    }
}
