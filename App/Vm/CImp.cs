using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Win32;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

public sealed class CanaryPatchImportVm : VmBase
{
    private readonly R3Inst? _install;
    private readonly AppFlow _flow;
    private readonly Func<bool> _isRpcs3Running;
    private readonly Func<string, string, bool> _confirm;

    private string _importText = string.Empty;
    private string _validationMessage = UiText.T("Patch.ImportCanaryPasteLabel");
    private string _resultMessage = string.Empty;
    private bool _isValid;
    private bool _duplicatePpuMerged;
    private bool _isRpcs3RunningPresentation;

    public CanaryPatchImportVm(
        R3Inst? install,
        AppFlow flow,
        Func<bool>? isRpcs3Running = null,
        Func<string, string, bool>? confirm = null)
    {
        _install = install;
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _isRpcs3Running = isRpcs3Running ??
            InstSvc.IsR3;
        _confirm = confirm ?? UiDlg.Confirm;

        ChooseFileCommand = new RelayCommand(ChooseFile);
        CreateFileCommand = new RelayCommand(
            CreateImportedPatchFile,
            () => _install is not null &&
                  IsValid &&
                  !_isRpcs3RunningPresentation);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));

        Validate();
        RefreshMutationState();
    }

    public event EventHandler<bool>? RequestClose;

    public RelayCommand ChooseFileCommand { get; }

    public RelayCommand CreateFileCommand { get; }

    public RelayCommand CancelCommand { get; }

    public string ImportText
    {
        get => _importText;
        set
        {
            if (SetProperty(ref _importText, value))
            {
                ResultMessage = string.Empty;
                Validate();
            }
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public string ResultMessage
    {
        get => _resultMessage;
        private set => SetProperty(ref _resultMessage, value);
    }

    public bool IsValid
    {
        get => _isValid;
        private set
        {
            if (SetProperty(ref _isValid, value))
            {
                CreateFileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool DuplicatePpuMerged
    {
        get => _duplicatePpuMerged;
        private set => SetProperty(ref _duplicatePpuMerged, value);
    }

    public FlowDirection WindowFlow => UiLaySvc.Current.WinFlow;

    public FlowDirection ShellFlow => UiLaySvc.Current.ShFlow;

    public XmlLanguage XmlLanguage => UiLaySvc.Current.XmlLang;

    public FlowDirection TextFlow => UiLaySvc.Current.TextFlow;

    public TextAlignment UiTextAlignment => UiLaySvc.Current.TxtAlign;

    public TextAlignment TextAlignment => UiTextAlignment;

    public HorizontalAlignment HorizontalAlignment => UiLaySvc.Current.HorAlign;

    public void RefreshMutationState()
    {
        try
        {
            _isRpcs3RunningPresentation =
                _install is not null && _isRpcs3Running();
        }
        catch
        {
            _isRpcs3RunningPresentation = true;
        }

        CreateFileCommand.RaiseCanExecuteChanged();
    }

    private void ChooseFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = UiText.T("Patch.ImportCanaryFileFilter"),
            Title = UiText.T("Patch.ImportCanaryChooseFile"),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ImportText = R3Bound.ReadUtf8(
                dialog.FileName,
                R3Limits.PatCatBy,
                "Canary import patch catalog");
        }
        catch (Exception ex) when (
            ex is InvalidDataException or
                IOException or
                UnauthorizedAccessException)
        {
            IsValid = false;
            ResultMessage = ex.Message;
        }
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(ImportText))
        {
            IsValid = false;
            DuplicatePpuMerged = false;
            ValidationMessage = UiText.T("Patch.ImportCanaryPasteLabel");
            return;
        }

        var result = _flow.NormalizeCanaryPatch(ImportText);

        IsValid = result.IsOk;
        DuplicatePpuMerged = result.PpuMerge;
        ValidationMessage = result.Message;
    }

    private void CreateImportedPatchFile()
    {
        if (!IsValid)
        {
            ResultMessage = ValidationMessage;
            return;
        }

        if (_install is null)
        {
            ResultMessage = UiText.T("Patch.ImportCanaryNoRpcs3Path");
            return;
        }

        RefreshMutationState();
        if (_isRpcs3RunningPresentation)
        {
            ResultMessage = UiText.T("Msg.BlockedRpcs3");
            return;
        }

        if (!_confirm(
                UiText.T("Patch.ImportCanaryConfirm"),
                UiText.T("Patch.ImportCanaryTitle")))
        {
            ResultMessage = UiText.T("Msg.UserCancelPatch");
            return;
        }

        var result = _flow.ImportCanaryPatch(_install, ImportText);
        ResultMessage = result.Message;

        if (result.IsOk)
        {
            RequestClose?.Invoke(this, true);
        }
    }
}
