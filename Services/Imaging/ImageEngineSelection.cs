namespace wwwmmm.Services.Imaging;

/// <summary>
/// Resolves which real image engine (L=Skia, R=ImageSharp) the runtime wiring uses.
/// Mirrors <see cref="wwwmmm.Services.StigmaProfiles"/>: explicit selection only,
/// no silent fallback, invalid values throw instead of being coerced.
/// </summary>
public static class ImageEngineSelection
{
    public const string EnvironmentVariable = "WWWMMM_IMAGE_ENGINE";

    public static ImageEngine FromEnvironment(ImageEngine fallback = ImageEngine.Skia)
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariable)?.Trim();
        if (string.IsNullOrEmpty(value))
            return fallback;

        if (Enum.TryParse<ImageEngine>(value, ignoreCase: true, out var named) &&
            Enum.IsDefined(typeof(ImageEngine), named))
            return named;

        throw new InvalidOperationException(
            $"{EnvironmentVariable} must be 'Skia' or 'ImageSharp'; received '{value}'.");
    }
}
