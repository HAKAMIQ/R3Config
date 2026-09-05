using R3Config.Lang;
using R3Config.Application;

namespace R3Config.Vm;

public sealed partial class MainVm
{
    public bool ShowCanaryVerificationPanel =>
        Preview.HasCan &&
        (NeedsCanaryFingerprint ||
         IsFingerprintCaptureRunning ||
         _fingerprintCaptureError != GameFingerprintCaptureError.None);

    public bool ShowCanaryVerificationAction =>
        Preview.HasCan &&
        NeedsCanaryFingerprint &&
        !IsFingerprintCaptureRunning;

    public bool ShowCanaryReverificationAction =>
        Preview.HasCan &&
        Preview.HasCanaryHashMismatch &&
        HasUnmatchedCanaryFingerprint &&
        !IsFingerprintCaptureRunning;

    public bool ShowCanaryVerificationText =>
        ShowCanaryVerificationPanel &&
        (NeedsCanaryFingerprint ||
         IsFingerprintCaptureRunning ||
         _fingerprintCaptureError != GameFingerprintCaptureError.None);

    public string CanaryVerificationText
    {
        get
        {
            if (_fingerprintCaptureError !=
                GameFingerprintCaptureError.None ||
                IsFingerprintCaptureRunning)
            {
                return FingerprintCaptureStatus;
            }

            if (NeedsCanaryFingerprint)
            {
                return FingerprintCaptureStatus;
            }

            return string.Empty;
        }
    }

    private void OnPatchPresentationChanged()
    {
        OnChange(nameof(ShowCanaryVerificationPanel));
        OnChange(nameof(ShowCanaryVerificationAction));
        OnChange(nameof(ShowCanaryReverificationAction));
        OnChange(nameof(ShowCanaryVerificationText));
        OnChange(nameof(CanaryVerificationText));
    }
}
