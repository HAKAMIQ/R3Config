using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

#pragma warning disable CA1822

public sealed class AdvVm : VmBase
{
    private string _cfgPath = "-";
    private string _custCfg = "-";
    private string _patPath = "-";
    private string _tgtCfg = "-";
    private string _logText = string.Empty;
    private string _catSrc = "-";
    private string _catVer = "-";
    private string _catTime = "-";
    private string _srcHash = "-";
    private string _recNum = "0";
    private string _devWiki = "-";
    private bool _isLocal;
    private BackupSnapshot? _selBkp;
    private readonly StringBuilder _log = new();

    public ObservableCollection<BackupSnapshot> Backups { get; } = [];

    public bool BkpEmpty => Backups.Count == 0;

    public string BkpText => UiText.GetText("Adv.EmptyBackups");

    public string CfgPath
    {
        get => _cfgPath;
        set => Set(ref _cfgPath, value);
    }

    public string CustCfg
    {
        get => _custCfg;
        set => Set(ref _custCfg, value);
    }

    public string PatPath
    {
        get => _patPath;
        set => Set(ref _patPath, value);
    }

    public string TgtCfg
    {
        get => _tgtCfg;
        set => Set(ref _tgtCfg, value);
    }

    public string CatSrc
    {
        get => _catSrc;
        set => Set(ref _catSrc, value);
    }

    public string CatVer
    {
        get => _catVer;
        set => Set(ref _catVer, value);
    }

    public string CatGenAt
    {
        get => _catTime;
        set => Set(ref _catTime, value);
    }

    public string SrcHash
    {
        get => _srcHash;
        set => Set(ref _srcHash, value);
    }

    public string RecNum
    {
        get => _recNum;
        set => Set(ref _recNum, value);
    }

    public string WikiPath
    {
        get => _devWiki;
        set => Set(ref _devWiki, value);
    }

    public BackupSnapshot? SelBkp
    {
        get => _selBkp;
        set => Set(ref _selBkp, value);
    }

    public string LogText
    {
        get => _logText;
        private set => Set(ref _logText, value);
    }

    public void SetLang()
    {
        if (_isLocal)
        {
            CatSrc = UiText.GetText("Cat.BuiltIn");
        }

        OnChange(nameof(BkpText));
    }

    public void SetCat(CatInfo? info, string unavailableMessage)
    {
        if (info is null)
        {
            _isLocal = false;
            CatSrc = unavailableMessage;
            CatVer = "-";
            CatGenAt = "-";
            SrcHash = "-";
            RecNum = "0";
            return;
        }

        _isLocal = string.Equals(
            info.DataSrc,
            "Built" + "-in",
            StringComparison.Ordinal);

        CatSrc = _isLocal
            ? UiText.GetText("Cat.BuiltIn")
            : info.DataSrc;

        CatVer = info.DataVer;
        CatGenAt = info.GenAt?.UtcDateTime.ToString(
            "yyyy-MM-dd HH:mm:ss 'UTC'",
            CultureInfo.InvariantCulture) ?? "-";
        SrcHash = string.IsNullOrWhiteSpace(info.SrcHash)
            ? "-"
            : info.SrcHash;
        RecNum = info.RecNum.ToString(CultureInfo.InvariantCulture);
    }

    public void Append(string message)
    {
        _log.AppendLine(
            CultureInfo.InvariantCulture,
            $"[{DateTime.Now:HH:mm:ss}] {message}");

        LogText = _log.ToString();
    }

    public void LoadBkp(IEnumerable<BackupSnapshot> backups)
    {
        Backups.Clear();

        foreach (var backup in backups)
        {
            Backups.Add(backup);
        }

        SelBkp = Backups.Count > 0
            ? Backups[0]
            : null;

        OnChange(nameof(BkpEmpty));
    }
}