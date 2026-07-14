namespace R3Config.Case;

public interface IPatMsg
{
    string MissCat { get; }

    string BadCat { get; }

    string FmtRead(
        string? rawError,
        bool isImported);
}
