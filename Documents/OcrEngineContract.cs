namespace wwwmmm.Documents;

/// <summary>
/// Reads text from a single page image. Implementations must fail loudly: an empty string
/// means the engine recognised no text, while a failure must throw so a caller can never
/// mistake "OCR broke" for "the page is blank".
/// </summary>
public interface IOcrEngine
{
    Task<string> ReadAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
