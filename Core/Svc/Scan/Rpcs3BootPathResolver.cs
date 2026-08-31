using R3Config.Mdl;

namespace R3Config.Svc;

#pragma warning disable CA1822

public sealed class Rpcs3BootPathResolver
{
    private const string GameIdBootPrefix = "%RPCS3_GAMEID%:";

    public string? Resolve(Rpcs3GameDescriptor game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var serial = game.Serial.Trim().ToUpperInvariant();
        if (serial.Length != 9 ||
            serial.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            return null;
        }

        return GameIdBootPrefix + serial;
    }
}

#pragma warning restore CA1822
