using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Win32;
using AppSvc;
using R3Config.Lang;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed class CanaryPatchImportVm : VmBase
{
    private readonly string? _patchesPath;
    private readonly CanaryPatchImportService _service = new();

    private string _importText = string.Empty;
    private string _validationMessage = UiText.T("Patch.ImportCanaryPasteLabel");
    private string _resultMessage = string.Empty;
    private bool _isValid;
    private bool _duplicatePpuMerged;

    public CanaryPatchImportVm(string? patchesPath)
    {
        _patchesPath = patchesPath;

        ChooseFileCommand = new RelayCommand(ChooseFile);
        CreateFileCommand = new RelayCommand(CreateImportedPatchFile, () => IsValid);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));

        Validate();
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

    public FlowDirection WindowFlow => UiLayoutService.Current.WindowFlow;

    public FlowDirection ShellFlow => UiLayoutService.Current.ShellFlow;

    public XmlLanguage XmlLanguage => UiLayoutService.Current.XmlLanguage;

    public FlowDirection TextFlow => UiLayoutService.Current.TextFlow;

    public TextAlignment UiTextAlignment => UiLayoutService.Current.TextAlignment;

    public TextAlignment TextAlignment => UiTextAlignment;

    public HorizontalAlignment HorizontalAlignment => UiLayoutService.Current.HorizontalAlignment;

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
            ImportText = File.ReadAllText(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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

        var result = _service.Normalize(ImportText);

        IsValid = result.Succeeded;
        DuplicatePpuMerged = result.DuplicatePpuMerged;
        ValidationMessage = result.Message;
    }

    private void CreateImportedPatchFile()
    {
        if (!IsValid)
        {
            ResultMessage = ValidationMessage;
            return;
        }

        if (string.IsNullOrWhiteSpace(_patchesPath))
        {
            ResultMessage = UiText.T("Patch.ImportCanaryNoRpcs3Path");
            return;
        }

        var result = _service.WriteImportedPatch(_patchesPath, ImportText);
        ResultMessage = result.Message;

        if (result.Succeeded)
        {
            RequestClose?.Invoke(this, true);
        }
    }
}
