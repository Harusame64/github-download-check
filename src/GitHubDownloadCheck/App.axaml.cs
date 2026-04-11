using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using GitHubDownloadCheck.Models;
using GitHubDownloadCheck.Services;
using GitHubDownloadCheck.ViewModels;
using GitHubDownloadCheck.Views;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace GitHubDownloadCheck;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // OS テーマに合わせて LiveCharts テーマを初期設定
        ApplyLiveChartsTheme(ActualThemeVariant);

        // OS テーマ変更時に LiveCharts テーマも追従
        ActualThemeVariantChanged += (_, _) => ApplyLiveChartsTheme(ActualThemeVariant);
    }

    private static void ApplyLiveChartsTheme(ThemeVariant? variant)
    {
        if (variant == ThemeVariant.Dark)
            LiveCharts.Configure(config => config.AddSkiaSharp().AddDefaultMappers().AddDarkTheme());
        else
            LiveCharts.Configure(config => config.AddSkiaSharp().AddDefaultMappers().AddLightTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();

            var apiService = new GitHubApiService();
            if (!string.IsNullOrWhiteSpace(settings.GitHubToken))
                apiService.SetToken(settings.GitHubToken);

            var cacheService = new CacheService();
            var analyticsService = new AnalyticsService();

            var dashboardVm = new DashboardViewModel(apiService, cacheService, analyticsService);
            var settingsVm = new SettingsViewModel(settingsService, settings);

            settingsVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.GitHubToken))
                    apiService.SetToken(settingsVm.GitHubToken);
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(dashboardVm, settingsVm, settings),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
