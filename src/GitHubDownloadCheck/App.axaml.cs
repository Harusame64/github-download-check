using System;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
    private DashboardViewModel? _dashboardViewModel;

    // 起動時の OS の言語。CurrentUICulture は上書きするので、"auto" の判定用に最初に控えておく
    private static readonly CultureInfo OsUICulture = CultureInfo.CurrentUICulture;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // OS テーマに合わせて LiveCharts テーマを初期設定
        ApplyLiveChartsTheme(ActualThemeVariant);

        // OS テーマ変更時に LiveCharts テーマも追従（チャートの色は構築時に決まるので作り直す）
        ActualThemeVariantChanged += (_, _) =>
        {
            ApplyLiveChartsTheme(ActualThemeVariant);
            _dashboardViewModel?.BuildCharts();
        };
    }

    // "auto" は OS の言語（日本語以外は英語にフォールバック）、"ja" / "en" はその言語に固定
    private static void ApplyCulture(string language)
    {
        var culture = language switch
        {
            "ja" => new CultureInfo("ja-JP"),
            "en" => new CultureInfo("en"),
            _ => OsUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase)
                ? OsUICulture
                : new CultureInfo("en"),
        };
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private static void ApplyLiveChartsTheme(ThemeVariant? variant)
    {
        if (variant == ThemeVariant.Dark)
            LiveCharts.Configure(config => config.AddSkiaSharp().AddDefaultMappers().AddDarkTheme().WithAnimationsSpeed(TimeSpan.FromMilliseconds(250)));
        else
            LiveCharts.Configure(config => config.AddSkiaSharp().AddDefaultMappers().AddLightTheme().WithAnimationsSpeed(TimeSpan.FromMilliseconds(250)));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            ApplyCulture(settings.Language);

            var apiService = new GitHubApiService();
            if (!string.IsNullOrWhiteSpace(settings.GitHubToken))
                apiService.SetToken(settings.GitHubToken);

            var cacheService = new CacheService();
            var analyticsService = new AnalyticsService();

            var dashboardVm = new DashboardViewModel(apiService, cacheService, analyticsService);
            var settingsVm = new SettingsViewModel(settingsService, settings);
            _dashboardViewModel = dashboardVm;

            settingsVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.GitHubToken))
                    apiService.SetToken(settingsVm.GitHubToken);
            };

            // {h:Localize} は読み込み時に一度だけ評価されるので、言語を変えたらウィンドウごと作り直す
            settingsVm.LanguageChanged += (_, language) =>
            {
                ApplyCulture(language);
                dashboardVm.RefreshLocalizedText();

                // ComboBox の選択変更の最中にウィンドウを閉じないよう、処理が戻ってから差し替える
                Dispatcher.UIThread.Post(() =>
                {
                    var old = desktop.MainWindow!;
                    var window = new MainWindow
                    {
                        DataContext = old.DataContext,
                        Width = old.Width,
                        Height = old.Height,
                        Position = old.Position,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        WindowState = old.WindowState,
                    };
                    desktop.MainWindow = window;
                    window.Show();
                    old.Close();
                    // 古いウィンドウの View が ViewModel のイベントを掴んだままにならないよう切り離す
                    old.DataContext = null;
                });
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(dashboardVm, settingsVm, settings),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
