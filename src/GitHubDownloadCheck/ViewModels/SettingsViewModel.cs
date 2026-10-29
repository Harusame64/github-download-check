using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    // 言語名はその言語自身で書く（どちらの表示でも自分の言語を見つけられるように）
    public IReadOnlyList<LanguageOption> LanguageOptions { get; } = new[]
    {
        new LanguageOption("auto"),
        new LanguageOption("ja", "日本語"),
        new LanguageOption("en", "English"),
    };

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    /// <summary>表示言語が変わったときに発火する。引数は "auto" / "ja" / "en"。</summary>
    public event EventHandler<string>? LanguageChanged;

    public SettingsViewModel(SettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
        _gitHubToken = settings.GitHubToken;
        Repositories = new ObservableCollection<RepositoryEntry>(settings.Repositories);
        _selectedLanguage = LanguageOptions.FirstOrDefault(o => o.Code == settings.Language) ?? LanguageOptions[0];
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value is null || value.Code == _settings.Language) return;
        _settings.Language = value.Code;
        Save();
        LanguageChanged?.Invoke(this, value.Code);
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

public record LanguageOption(string Code, string? FixedLabel = null)
{
    // 「自動」だけは表示中の言語で出す。切替後にウィンドウを作り直したとき読み直されるようプロパティで返す
    public string Label => FixedLabel ?? Strings.Settings_LanguageAuto;
}
