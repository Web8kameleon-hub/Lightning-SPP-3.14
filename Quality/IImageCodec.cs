namespace wwwmmm.Quality;

public interface IImageCodec
{
    string Name { get; }
    byte[] Encode(ImagePixelBuffer image);
    ImagePixelBuffer Decode(byte[] encoded);
}
