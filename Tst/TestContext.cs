using System.IO;

namespace R3Config.Tests;

internal sealed class TestContext
{
    public string? TempRoot { get; private set; }

    public string CreateTempRoot(string prefix)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            prefix,
            Guid.NewGuid().ToString("N"));
        TempRoot = path;
        Directory.CreateDirectory(path);
        return path;
    }

    public void Cleanup()
    {
        var tempRoot = TempRoot;
        if (tempRoot is not null &&
            Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
