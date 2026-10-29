using System;
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
    private DashboardViewModel? _vm;
    private bool _redrawQueued;

    public DashboardView()
    {
        InitializeComponent();
        _charts = new[] { TrendChart, PeriodChart, ReleaseChart, AssetChart };

        // スクロール時にチャートの描画を強制的に更新（高速スクロール時の描画漏れ対策）
        MainScrollViewer.ScrollChanged += (_, _) =>
        {
            foreach (var chart in _charts)
                chart.InvalidateVisual();
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
            QueueRedraw();
    }

    private void OnChartsRebuilt(object? sender, EventArgs e) => QueueRedraw();

    // LiveCharts は非表示中に Series が差し替わると、再表示されてもサイズが同じなら描き直さない。
    // そのため前のリポジトリのグラフが残って見えることがあった。レイアウト確定後に明示的に更新する
    private void QueueRedraw()
    {
        if (_redrawQueued) return;
        _redrawQueued = true;

        Dispatcher.UIThread.Post(() =>
        {
            _redrawQueued = false;
            foreach (var chart in _charts)
            {
                if (!chart.IsEffectivelyVisible) continue;
                chart.CoreChart.Update(new ChartUpdateParams { IsAutomaticUpdate = false, Throttling = false });
            }
        }, DispatcherPriority.Background);
    }
}
