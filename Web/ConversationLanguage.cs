using System.Globalization;
using System.Text.Json;

namespace wwwmmm.Web;

internal static class ConversationLanguage
{
    internal static string RoutingLanguage(JsonElement request) =>
        request.TryGetProperty("responseLanguage", out var selected) ? Canonical(selected) : "und";

    // This is language metadata, never a free-form instruction or a provider name.
    internal static string Instruction(JsonElement request)
    {
        const string automatic = "Respond in the language of the user's latest message unless the user explicitly requests another language. Preserve technical identifiers, code and source quotations as needed. ";
        if (request.TryGetProperty("responseLanguage", out var selected))
        {
            var language = Canonical(selected);
            return "Respond in the explicitly selected language " + CultureInfo.GetCultureInfo(language).EnglishName + " (" + language + ")" + ". Preserve technical identifiers, code and source quotations. If you cannot reliably answer in that language, disclose that limitation instead of inventing a translation. ";
        }
        if (!request.TryGetProperty("locale", out var value)) return automatic;
        return automatic + "For an ambiguous message with no identifiable language, use the interface locale " + CultureInfo.GetCultureInfo(Canonical(value)).EnglishName + " (" + Canonical(value) + ")" + ". This preference is not evidence that the model supports that language. ";
    }

    private static string Canonical(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String) throw new ArgumentException("Locale must be a language tag.");
        var tag = value.GetString()!;
        if (tag.Length is < 1 or > 85 || tag.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Locale must be a valid language tag.");
        string canonical;
        try { canonical = CultureInfo.GetCultureInfo(tag).Name; }
        catch (CultureNotFoundException) { throw new ArgumentException("Locale must be a valid language tag."); }
        if (canonical.Length == 0) throw new ArgumentException("Locale must identify a language.");
        return canonical;
    }
}
