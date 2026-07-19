using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public enum GameLaunchError
{
    None,
    Rpcs3Missing,
    GameIdentityMissing,
    LaunchFailed
}

public sealed record GameLaunchResult(
    bool Started,
    GameLaunchError Error,
    string? BootTarget = null,
    string? Detail = null);

public interface IRpcs3GameProcessLauncher
{
    void Start(
        string executablePath,
        string workingDirectory,
        string bootTarget);
}

public sealed class GameLaunchUseCase
{
    private readonly Rpcs3BootPathResolver _bootResolver;
    private readonly IRpcs3GameProcessLauncher _launcher;

    public GameLaunchUseCase(
        Rpcs3BootPathResolver bootResolver,
        IRpcs3GameProcessLauncher launcher)
    {
        _bootResolver = bootResolver ??
            throw new ArgumentNullException(nameof(bootResolver));
        _launcher = launcher ??
            throw new ArgumentNullException(nameof(launcher));
    }

    public GameLaunchResult Launch(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);

        if (!File.Exists(install.ExePath))
        {
            return new GameLaunchResult(
                false,
                GameLaunchError.Rpcs3Missing,
                Detail: install.ExePath);
        }

        var bootTarget = _bootResolver.Resolve(game);
        if (bootTarget is null)
        {
            return new GameLaunchResult(
                false,
                GameLaunchError.GameIdentityMissing,
                Detail: game.Serial);
        }

        try
        {
            _launcher.Start(
                Path.GetFullPath(install.ExePath),
                Path.GetFullPath(install.RootPath),
                bootTarget);
            return new GameLaunchResult(
                true,
                GameLaunchError.None,
                bootTarget);
        }
        catch (Exception ex)
        {
            return new GameLaunchResult(
                false,
                GameLaunchError.LaunchFailed,
                bootTarget,
                ex.Message);
        }
    }
}
