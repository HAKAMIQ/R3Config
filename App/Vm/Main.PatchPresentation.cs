using R3Config.Lang;
using R3Config.Application;

namespace R3Config.Vm;

public sealed partial class MainVm
{
    public bool ShowCanaryVerificationPanel =>
        Preview.HasCan &&
        ((Preview.NeedsCanaryVerification &&
          NeedsCanaryFingerprint) ||
         IsFingerprintCaptureRunning ||
         _fingerprintCaptureError != GameFingerprintCaptureError.None ||
         (Preview.HasCanaryHashMismatch &&
          HasUnmatchedCanaryFingerprint));

    public bool ShowCanaryVerificationAction =>
        Preview.HasCan &&
        Preview.NeedsCanaryVerification &&
        NeedsCanaryFingerprint &&
        !IsFingerprintCaptureRunning;

    public bool ShowCanaryVerificationText =>
        ShowCanaryVerificationPanel &&
        (IsFingerprintCaptureRunning ||
         _fingerprintCaptureError != GameFingerprintCaptureError.None ||
         (Preview.HasCanaryHashMismatch &&
          HasUnmatchedCanaryFingerprint));

    public string CanaryVerificationText
    {
        get
        {
            if (Preview.HasCanaryHashMismatch &&
                HasUnmatchedCanaryFingerprint)
            {
                return UiText.GetText("PatchReadiness.VerifyMismatch");
            }

            if (_fingerprintCaptureError !=
                GameFingerprintCaptureError.None ||
                IsFingerprintCaptureRunning)
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
        OnChange(nameof(ShowCanaryVerificationText));
        OnChange(nameof(CanaryVerificationText));
    }
}
