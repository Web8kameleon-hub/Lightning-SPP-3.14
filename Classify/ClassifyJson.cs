using System.Text.Json;

namespace wwwmmm.Classify;

/// <summary>
/// Serializer settings shared by the taxonomy loader and the run manifest, so a file written by
/// one command is readable by another without guessing property names.
/// </summary>
internal static class ClassifyJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
