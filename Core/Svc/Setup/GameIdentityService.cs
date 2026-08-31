using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

#pragma warning disable CA1822

public sealed partial class GameIdentityService
{
    public GameIdentity Resolve(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);

        var serial = game.Serial.Trim().ToUpperInvariant();
        if (!Serial().IsMatch(serial))
        {
            throw new InvalidDataException(
                $"Installed game serial '{game.Serial}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(game.Title) ||
            string.IsNullOrWhiteSpace(game.InstPath))
        {
            throw new InvalidDataException(
                "Installed game identity is missing title or install path evidence.");
        }

        var customConfigPath = Path.GetFullPath(Path.Combine(
            install.CustPath,
            $"config_{serial}.yml"));
        var expectedRoot = Path.GetFullPath(install.CustPath)
            .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!customConfigPath.StartsWith(
                expectedRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The resolved custom configuration path is outside this RPCS3 installation.");
        }

        var version = PatVer.Norm(game.Version);
        var hashEvidence = game.HashEvi switch
        {
            HashEvi.PlainLog or HashEvi.ZipLog =>
                SetupIdentityEvidence.RuntimeLog,
            HashEvi.Cache => SetupIdentityEvidence.RuntimeCache,
            HashEvi.Meta => SetupIdentityEvidence.InstalledMetadata,
            _ => SetupIdentityEvidence.Unavailable
        };

        return new GameIdentity(
            game.Title.Trim(),
            serial,
            version,
            Path.GetFullPath(game.InstPath),
            customConfigPath,
            game.PpuList,
            game.SpuList,
            game.PrxList,
            game.OvlList,
            version is null
                ? SetupIdentityEvidence.Unavailable
                : SetupIdentityEvidence.InstalledMetadata,
            hashEvidence);
    }

    [GeneratedRegex(
        "^[A-Z]{4}[0-9]{5}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Serial();
}

#pragma warning restore CA1822
