using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitHubDownloadCheck.Models;
using GitHubDownloadCheck.Services;

namespace GitHubDownloadCheck.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings _settings;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private bool _isSettingsOpen;

    public DashboardViewModel DashboardViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public ObservableCollection<RepositoryEntry> Repositories { get; }

    public MainWindowViewModel(
        DashboardViewModel dashboardViewModel,
        SettingsViewModel settingsViewModel,
        AppSettings settings)
    {
        DashboardViewModel = dashboardViewModel;
        SettingsViewModel = settingsViewModel;
        _settings = settings;
        Repositories = settingsViewModel.Repositories;
        CurrentView = dashboardViewModel;

        // 設定画面を開いたままサイドバーでリポジトリを選んだら、ダッシュボードに戻す
        dashboardViewModel.PropertyChanged += (_, e) =>
        {
            // 設定画面で選択中のリポジトリを削除したときは null になる。そのときは設定画面に留まる
            if (e.PropertyName == nameof(DashboardViewModel.SelectedRepository)
                && IsSettingsOpen
                && dashboardViewModel.SelectedRepository is not null)
                CloseSettings();
        };
    }

    [RelayCommand]
    private void SelectRepository(RepositoryEntry repo)
    {
        DashboardViewModel.SelectedRepository = repo;
        IsSettingsOpen = false;
        CurrentView = DashboardViewModel;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        IsSettingsOpen = true;
        CurrentView = SettingsViewModel;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
        CurrentView = DashboardViewModel;
    }
}
