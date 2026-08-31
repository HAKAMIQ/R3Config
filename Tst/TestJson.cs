using System.Text.Json;
using System.Text.Json.Serialization;

namespace R3Config.Tests;

internal static class TestJson
{
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true
    };

    public static readonly JsonSerializerOptions IndentedIgnoreNull = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
