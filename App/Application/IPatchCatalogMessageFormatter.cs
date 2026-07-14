namespace R3Config.UseCases;

public interface IPatchCatalogMessageFormatter
{
    string MissingCatalog { get; }

    string InvalidCatalog { get; }

    string FormatReadError(
        string? rawError,
        bool isImported);
}
