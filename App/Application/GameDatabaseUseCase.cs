using R3Config.Svc;
using System.IO;
using System.Text.Json;

namespace R3Config.Application;

public sealed record GameDatabaseLoadResult(
    Rpcs3GameDatabase Database,
    string? Error);

public sealed class GameDatabaseUseCase
{
#pragma warning disable CA1822 // Stateless application boundary, consistent with the other data use cases.
    public GameDatabaseLoadResult Load(string baseDirectory)
    {
        try
        {
            return new GameDatabaseLoadResult(
                Rpcs3GameDatabase.Load(Path.Combine(baseDirectory, "Data")),
                null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            return new GameDatabaseLoadResult(
                Rpcs3GameDatabase.Empty,
                $"RPCS3 game database could not be loaded: {ex.Message}");
        }
    }
#pragma warning restore CA1822
}
