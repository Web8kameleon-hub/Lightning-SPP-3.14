namespace wwwmmm.Quality;

public interface IImageQualityMetric
{
    string Name { get; }
    double Score(ImagePixelBuffer reference, ImagePixelBuffer candidate);
}
