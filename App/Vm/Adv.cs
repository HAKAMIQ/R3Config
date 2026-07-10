using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

public sealed class AdvancedVm : VmBase
{
    private string _configPath = "-";
    private string _customConfigsPath = "-";
    private string _patchesPath = "-";
    private string _targetConfigPath = "-";
    private string _logText = string.Empty;
    private string _catalogSource = "-";
    private string _catalogVersion = "-";
    private string _catalogGeneratedAt = "-";
    private string _catalogSourceHash = "-";
    private string _catalogRecommendationCount = "0";
    private string _developerWikiPath = "-";
    private bool _catalogSourceIsBuiltIn;
    private BackupSnapshot? _selectedBackup;
    private readonly StringBuilder _log = new();

    public ObservableCollection<BackupSnapshot> Backups { get; } = [];

    public bool IsBackupsEmpty => Backups.Count == 0;

    public string EmptyBackupsText => UiText.T("Adv.EmptyBackups");

    public string ConfigPath
    {
        get => _configPath;
        set => SetProperty(ref _configPath, value);
    }

    public string CustomConfigsPath
    {
        get => _customConfigsPath;
        set => SetProperty(ref _customConfigsPath, value);
    }

    public string PatchesPath
    {
        get => _patchesPath;
        set => SetProperty(ref _patchesPath, value);
    }

    public string TargetConfigPath
    {
        get => _targetConfigPath;
        set => SetProperty(ref _targetConfigPath, value);
    }

    public string CatalogSource
    {
        get => _catalogSource;
        set => SetProperty(ref _catalogSource, value);
    }

    public string CatalogVersion
    {
        get => _catalogVersion;
        set => SetProperty(ref _catalogVersion, value);
    }

    public string CatalogGeneratedAt
    {
        get => _catalogGeneratedAt;
        set => SetProperty(ref _catalogGeneratedAt, value);
    }

    public string CatalogSourceHash
    {
        get => _catalogSourceHash;
        set => SetProperty(ref _catalogSourceHash, value);
    }

    public string CatalogRecommendationCount
    {
        get => _catalogRecommendationCount;
        set => SetProperty(ref _catalogRecommendationCount, value);
    }

    public string DeveloperWikiPath
    {
        get => _developerWikiPath;
        set => SetProperty(ref _developerWikiPath, value);
    }

    public BackupSnapshot? SelectedBackup
    {
        get => _selectedBackup;
        set => SetProperty(ref _selectedBackup, value);
    }

    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    public void RefreshLanguage()
    {
        if (_catalogSourceIsBuiltIn)
        {
            CatalogSource = UiText.T("Cat.BuiltIn");
        }

        OnPropertyChanged(nameof(EmptyBackupsText));
    }

    public void SetCatalogInfo(CatalogInfo? info, string unavailableMessage)
    {
        if (info is null)
        {
            _catalogSourceIsBuiltIn = false;
            CatalogSource = unavailableMessage;
            CatalogVersion = "-";
            CatalogGeneratedAt = "-";
            CatalogSourceHash = "-";
            CatalogRecommendationCount = "0";
            return;
        }

        _catalogSourceIsBuiltIn = string.Equals(
            info.DataSource,
            "Built" + "-in",
            StringComparison.Ordinal);

        CatalogSource = _catalogSourceIsBuiltIn
            ? UiText.T("Cat.BuiltIn")
            : info.DataSource;

        CatalogVersion = info.DataVersion;
        CatalogGeneratedAt = info.GeneratedAt?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "-";
        CatalogSourceHash = string.IsNullOrWhiteSpace(info.SourceHash) ? "-" : info.SourceHash;
        CatalogRecommendationCount = info.RecommendationCount.ToString(CultureInfo.InvariantCulture);
    }

    public void AppendLog(string message)
    {
        _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogText = _log.ToString();
    }

    public void RefreshBackups(IEnumerable<BackupSnapshot> backups)
    {
        Backups.Clear();

        foreach (var backup in backups)
        {
            Backups.Add(backup);
        }

        SelectedBackup = Backups.Count > 0 ? Backups[0] : null;
        OnPropertyChanged(nameof(IsBackupsEmpty));
    }
}