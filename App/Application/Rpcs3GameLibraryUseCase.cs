using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class Rpcs3GameLibraryUseCase(
    GameScanner scanner)
{
    public async Task<GameScanResult> ScanAsync(
        string rpcs3ExePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var install = Rpcs3InstallService.FromExe(
                rpcs3ExePath);
            var scan = scanner.ScanInfo(
                install,
                cancellationToken);

            return scan;
        }, cancellationToken).ConfigureAwait(false);
    }
}
