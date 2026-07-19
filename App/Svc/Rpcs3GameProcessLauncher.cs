using System.Diagnostics;
using R3Config.Application;

namespace R3Config.Svc;

public sealed class Rpcs3GameProcessLauncher : IRpcs3GameProcessLauncher
{
    public void Start(
        string executablePath,
        string workingDirectory,
        string bootTarget)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(bootTarget);

        if (Process.Start(startInfo) is null)
        {
            throw new InvalidOperationException(
                "RPCS3 did not create a process.");
        }
    }
}
