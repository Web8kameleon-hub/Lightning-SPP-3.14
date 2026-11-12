using wwwmmm.Quality;

namespace wwwmmm.Services.Imaging;

public static class ImageEngineCandidates
{
    public static EngineCandidate For(
        ImageEngineAdapter adapter,
        ImageEngine engine,
        IImageCodec codec,
        ScanMode mode = ScanMode.Lightning,
        int dpi = 300)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(codec);
        var id = engine.ToString();
        return new EngineCandidate(id, async (image, ct) =>
        {
            var encoded = codec.Encode(image);
            var result = await adapter.ProcessAsync(engine, encoded, mode, dpi, ct)
                                       .ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException($"{id} failed: {result.Error ?? "(no message)"}");
            if (result.Data is null || result.Data.Length == 0)
                throw new InvalidOperationException($"{id} returned empty output.");
            return codec.Decode(result.Data);
        });
    }
}
