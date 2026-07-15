namespace R3Config.Application;

public interface IPatchCatalogMessageFormatter
{
    string MissCat { get; }

    string BadCat { get; }

    string FmtRead(
        string? rawError,
        bool isImported);
}
