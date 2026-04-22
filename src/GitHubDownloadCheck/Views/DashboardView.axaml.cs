using Avalonia.Controls;
using LiveChartsCore.SkiaSharpView.Avalonia;
using Avalonia.VisualTree;
using System.Linq;

namespace GitHubDownloadCheck.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        
        // スクロール時にチャートの描画を強制的に更新（高速スクロール時の描画漏れ対策）
        MainScrollViewer.ScrollChanged += (s, e) =>
        {
            var charts = this.GetVisualDescendants().OfType<CartesianChart>();
            foreach (var chart in charts)
            {
                chart.InvalidateVisual();
            }
        };
    }
}
