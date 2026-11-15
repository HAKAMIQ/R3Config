using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

#pragma warning disable CA1822

public sealed class CanImpVm : VmBase
{
    private readonly Rpcs3InstallDescriptor? _install;
    private readonly AppFlow _flow;
    private readonly Func<bool> _isRun;
    private readonly Func<string, string, bool> _confirm;
    private readonly Func<string?> _pickFile;

    private string _impText = string.Empty;
    private string _vldMsg = UiText.GetText("Patch.ImportCanaryPasteLabel");
    private string _resMsg = string.Empty;
    private bool _isValid;
    private bool _dupPpu;
    private bool _runUi;

    public CanImpVm(
        Rpcs3InstallDescriptor? install,
        AppFlow flow,
        Func<bool>? isRpcs3Running = null,
        Func<string, string, bool>? confirm = null,
        Func<string?>? pickFile = null)
    {
        _install = install;
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _isRun = isRpcs3Running ??
            Rpcs3InstallService.IsR3;
        _confirm = confirm ?? UiDlg.Confirm;
        _pickFile = pickFile ?? UiDlg.PickCan;

        PickCmd = new RelayCmd(PickFile);
        MakeCmd = new RelayCmd(
            Build,
            () => _install is not null &&
                  IsValid &&
                  !_runUi);
        CloseCmd = new RelayCmd(() => CloseReq?.Invoke(this, false));

        Validate();
        LoadMut();
    }

    public event EventHandler<bool>? CloseReq;

    public RelayCmd PickCmd { get; }

    public RelayCmd MakeCmd { get; }

    public RelayCmd CloseCmd { get; }

    public string ImpText
    {
        get => _impText;
        set
        {
            if (Set(ref _impText, value))
            {
                ResMsg = string.Empty;
                Validate();
            }
        }
    }

    public string VldMsg
    {
        get => _vldMsg;
        private set => Set(ref _vldMsg, value);
    }

    public string ResMsg
    {
        get => _resMsg;
        private set => Set(ref _resMsg, value);
    }

    public bool IsValid
    {
        get => _isValid;
        private set
        {
            if (Set(ref _isValid, value))
            {
                MakeCmd.RaiseCan();
            }
        }
    }

    public bool DupPpu
    {
        get => _dupPpu;
        private set => Set(ref _dupPpu, value);
    }

    public FlowDirection WinFlow => UiLaySvc.Current.WinFlow;

    public FlowDirection ShFlow => UiLaySvc.Current.ShFlow;

    public XmlLanguage XmlLang => UiLaySvc.Current.XmlLang;

    public FlowDirection TextFlow => UiLaySvc.Current.TextFlow;

    public TextAlignment UiAlign => UiLaySvc.Current.TxtAlign;

    public TextAlignment TxtAlign => UiAlign;

    public HorizontalAlignment HorAlign => UiLaySvc.Current.HorAlign;

    public void LoadMut()
    {
        try
        {
            _runUi =
                _install is not null && _isRun();
        }
        catch
        {
            _runUi = true;
        }

        MakeCmd.RaiseCan();
    }

    private void PickFile()
    {
        if (_pickFile() is not { } path)
        {
            return;
        }

        try
        {
            ImpText = R3Bound.ReadUtf8(
                path,
                R3Limits.PatCatBy,
                "Canary import patch catalog");
        }
        catch (Exception ex) when (
            ex is InvalidDataException or
                IOException or
                UnauthorizedAccessException)
        {
            IsValid = false;
            ResMsg = ex.Message;
        }
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(ImpText))
        {
            IsValid = false;
            DupPpu = false;
            VldMsg = UiText.GetText("Patch.ImportCanaryPasteLabel");
            return;
        }

        var result = _flow.NormCan(ImpText);

        IsValid = result.IsOk;
        DupPpu = result.PpuMerge;
        VldMsg = result.Message;
    }

    private void Build()
    {
        if (!IsValid)
        {
            ResMsg = VldMsg;
            return;
        }

        if (_install is null)
        {
            ResMsg = UiText.GetText("Patch.ImportCanaryNoRpcs3Path");
            return;
        }

        LoadMut();
        if (_runUi)
        {
            ResMsg = UiText.GetText("Msg.BlockedRpcs3");
            return;
        }

        if (!_confirm(
                UiText.GetText("Patch.ImportCanaryConfirm"),
                UiText.GetText("Patch.ImportCanaryTitle")))
        {
            ResMsg = UiText.GetText("Msg.UserCancelPatch");
            return;
        }

        var result = _flow.ImpCan(_install, ImpText);
        ResMsg = result.Message;

        if (result.IsOk)
        {
            CloseReq?.Invoke(this, true);
        }
    }
}