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
