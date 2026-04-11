using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitHubDownloadCheck.Models;
using GitHubDownloadCheck.Resources;
using GitHubDownloadCheck.Services;

namespace GitHubDownloadCheck.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private string _gitHubToken;

    [ObservableProperty]
    private string _newOwner = "";

    [ObservableProperty]
    private string _newRepo = "";

    [ObservableProperty]
    private string _newDisplayName = "";

    [ObservableProperty]
    private RepositoryEntry? _selectedRepository;

    [ObservableProperty]
    private string _statusMessage = "";

    public ObservableCollection<RepositoryEntry> Repositories { get; }

    public SettingsViewModel(SettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
        _gitHubToken = settings.GitHubToken;
        Repositories = new ObservableCollection<RepositoryEntry>(settings.Repositories);
    }

    [RelayCommand]
    private void AddRepository()
    {
        if (string.IsNullOrWhiteSpace(NewOwner) || string.IsNullOrWhiteSpace(NewRepo))
        {
            StatusMessage = Strings.Settings_ValidationOwnerRepo;
            return;
        }

        var entry = new RepositoryEntry
        {
            Owner = NewOwner.Trim(),
            Repo = NewRepo.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(NewDisplayName) ? null : NewDisplayName.Trim()
        };

        Repositories.Add(entry);
        _settings.Repositories.Add(entry);
        Save();

        NewOwner = "";
        NewRepo = "";
        NewDisplayName = "";
        StatusMessage = string.Format(Strings.Settings_AddedMessage, entry.Label);
    }

    [RelayCommand]
    private void RemoveRepository(RepositoryEntry entry)
    {
        Repositories.Remove(entry);
        _settings.Repositories.Remove(entry);
        Save();
        StatusMessage = string.Format(Strings.Settings_RemovedMessage, entry.Label);
    }

    [RelayCommand]
    private void SaveToken()
    {
        _settings.GitHubToken = GitHubToken.Trim();
        Save();
        StatusMessage = Strings.Settings_PATSaved;
    }

    private void Save() => _settingsService.Save(_settings);
}
