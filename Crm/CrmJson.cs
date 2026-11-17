using System.Text.Json;

namespace wwwmmm.Crm;

/// <summary>
/// One serializer configuration for every CRM record, so a value written by one command is
/// always readable by another. Indented output keeps the on-disk files auditable by hand.
/// </summary>
internal static class CrmJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
}
