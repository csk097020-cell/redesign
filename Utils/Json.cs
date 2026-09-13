// GENERAL-PURPOSE COMPONENT - Licensed to Client per contract Section 6.2
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MomentaryMomentos.Utils;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
