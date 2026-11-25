//! Safe Rust operations. Everything here works on real pixel data.

use image::{
    codecs::jpeg::JpegEncoder,
    imageops::FilterType,
    DynamicImage, GrayImage, ImageFormat, ImageReader,
};
use std::io::{Cursor, Read, Write};

pub const OP_NONE: u32 = 0;
pub const OP_GRAYSCALE: u32 = 1;
pub const OP_SOBEL: u32 = 2;
pub const OP_SHARPEN: u32 = 3;
pub const OP_RESIZE_HALF: u32 = 4;
pub const OP_GAUSSIAN_BLUR: u32 = 5;
pub const OP_ROTATE_180: u32 = 6;

pub const FMT_PNG: u32 = 1;
pub const FMT_JPEG: u32 = 2;
pub const FMT_BMP: u32 = 3;
pub const FMT_TIFF: u32 = 4;

/// Upper bound for a decoded image, so a hostile header cannot exhaust memory.
const MAX_PIXELS: u64 = 80_000_000;

#[derive(Debug)]
pub struct CoreError(pub String);

impl std::fmt::Display for CoreError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.0)
    }
}

impl std::error::Error for CoreError {}

fn err<T: Into<String>>(message: T) -> CoreError {
    CoreError(message.into())
}

/// Decodes real bytes. Returns an error for anything that is not a supported image.
pub fn decode(bytes: &[u8]) -> Result<DynamicImage, CoreError> {
    if bytes.is_empty() {
        return Err(err("empty image input"));
    }
    let reader = ImageReader::new(Cursor::new(bytes))
        .with_guessed_format()
        .map_err(|e| err(format!("format guess failed: {e}")))?;
    let image = reader.decode().map_err(|e| err(format!("decode failed: {e}")))?;
    let pixels = u64::from(image.width()) * u64::from(image.height());
    if pixels == 0 || pixels > MAX_PIXELS {
        return Err(err(format!("unsupported image size: {pixels} pixels")));
    }
    Ok(image)
}

/// Encodes to a real target format. JPEG quality is clamped to the format's valid range.
pub fn encode(image: &DynamicImage, format: u32, quality: u32) -> Result<Vec<u8>, CoreError> {
    let mut buffer: Vec<u8> = Vec::new();
    match format {
        FMT_PNG => image
            .write_to(&mut Cursor::new(&mut buffer), ImageFormat::Png)
            .map_err(|e| err(format!("png encode failed: {e}")))?,
        FMT_JPEG => {
            let clamped = quality.clamp(1, 100) as u8;
            let mut cursor = Cursor::new(&mut buffer);
            let mut encoder = JpegEncoder::new_with_quality(&mut cursor, clamped);
            encoder
                .encode_image(image)
                .map_err(|e| err(format!("jpeg encode failed: {e}")))?;
        }
        FMT_BMP => image
            .write_to(&mut Cursor::new(&mut buffer), ImageFormat::Bmp)
            .map_err(|e| err(format!("bmp encode failed: {e}")))?,
        FMT_TIFF => image
            .write_to(&mut Cursor::new(&mut buffer), ImageFormat::Tiff)
            .map_err(|e| err(format!("tiff encode failed: {e}")))?,
        other => return Err(err(format!("unsupported output format: {other}"))),
    }
    if buffer.is_empty() {
        return Err(err("encoder produced no bytes"));
    }
    Ok(buffer)
}
/// Real 3x3 Sobel gradient magnitude on grayscale pixels.
fn sobel(gray: &GrayImage) -> GrayImage {
    let width = gray.width() as usize;
    let height = gray.height() as usize;
    let source = gray.as_raw();
    let mut out = vec![0u8; width * height];
    if width >= 3 && height >= 3 {
        for y in 1..height - 1 {
            for x in 1..width - 1 {
                let at = |dx: isize, dy: isize| -> i32 {
                    let column = (x as isize + dx) as usize;
                    let row = (y as isize + dy) as usize;
                    i32::from(source[row * width + column])
                };
                let gx = -at(-1, -1) + at(1, -1) - 2 * at(-1, 0) + 2 * at(1, 0) - at(-1, 1) + at(1, 1);
                let gy = -at(-1, -1) - 2 * at(0, -1) - at(1, -1) + at(-1, 1) + 2 * at(0, 1) + at(1, 1);
                let magnitude = ((gx * gx + gy * gy) as f32).sqrt().min(255.0) as u8;
                out[y * width + x] = magnitude;
            }
        }
    }
    GrayImage::from_raw(width as u32, height as u32, out).expect("buffer size matches dimensions")
}

/// Normalized Gaussian kernel for the given sigma.
fn gaussian_kernel(sigma: f32) -> Vec<f32> {
    let radius = (3.0_f32 * sigma).ceil() as usize;
    let two_sigma_squared = 2.0 * sigma * sigma;
    let mut kernel = Vec::with_capacity(2 * radius + 1);
    let mut sum = 0.0_f32;
    for i in 0..=(2 * radius) {
        let x = i as f32 - radius as f32;
        let weight = (-(x * x) / two_sigma_squared).exp();
        kernel.push(weight);
        sum += weight;
    }
    if sum > 0.0 {
        for weight in kernel.iter_mut() {
            *weight /= sum;
        }
    }
    kernel
}

/// Separable convolution: two real passes (horizontal, then vertical).
fn convolve_separable(source: &[u8], width: usize, height: usize, kernel: &[f32]) -> Vec<u8> {
    let radius = (kernel.len() / 2) as isize;
    let mut horizontal = vec![0.0_f32; width * height];
    for y in 0..height {
        for x in 0..width {
            let mut total = 0.0_f32;
            for (i, weight) in kernel.iter().enumerate() {
                let column = (x as isize + i as isize - radius).clamp(0, width as isize - 1) as usize;
                total += f32::from(source[y * width + column]) * weight;
            }
            horizontal[y * width + x] = total;
        }
    }
    let mut out = vec![0u8; width * height];
    for y in 0..height {
        for x in 0..width {
            let mut total = 0.0_f32;
            for (i, weight) in kernel.iter().enumerate() {
                let row = (y as isize + i as isize - radius).clamp(0, height as isize - 1) as usize;
                total += horizontal[row * width + x] * weight;
            }
            out[y * width + x] = total.clamp(0.0, 255.0) as u8;
        }
    }
    out
}

fn gaussian_blur(gray: &GrayImage, sigma: f32) -> GrayImage {
    let width = gray.width() as usize;
    let height = gray.height() as usize;
    let kernel = gaussian_kernel(sigma.max(0.1));
    let blurred = convolve_separable(gray.as_raw(), width, height, &kernel);
    GrayImage::from_raw(width as u32, height as u32, blurred).expect("buffer size matches dimensions")
}

/// Real unsharp mask: out = source + amount * (source - blurred).
fn unsharp_mask(gray: &GrayImage, sigma: f32, amount: f32) -> GrayImage {
    let width = gray.width() as usize;
    let height = gray.height() as usize;
    let blurred = gaussian_blur(gray, sigma);
    let source = gray.as_raw();
    let blur = blurred.as_raw();
    let mut out = vec![0u8; width * height];
    for i in 0..width * height {
        let s = f32::from(source[i]);
        let b = f32::from(blur[i]);
        out[i] = (s + amount * (s - b)).clamp(0.0, 255.0) as u8;
    }
    GrayImage::from_raw(width as u32, height as u32, out).expect("buffer size matches dimensions")
}

/// Applies one real operation and re-encodes the result as PNG.
pub fn process(input: &[u8], op: u32, param: u32) -> Result<Vec<u8>, CoreError> {
    let image = decode(input)?;
    let processed: DynamicImage = match op {
        OP_NONE => image,
        OP_GRAYSCALE => DynamicImage::ImageLuma8(image.to_luma8()),
        OP_SOBEL => DynamicImage::ImageLuma8(sobel(&image.to_luma8())),
        OP_SHARPEN => {
            let amount = (param as f32 / 10.0).clamp(0.0, 5.0);
            DynamicImage::ImageLuma8(unsharp_mask(&image.to_luma8(), 1.0, if amount > 0.0 { amount } else { 1.0 }))
        }
        OP_RESIZE_HALF => {
            let width = (image.width() / 2).max(1);
            let height = (image.height() / 2).max(1);
            image.resize_exact(width, height, FilterType::Lanczos3)
        }
        OP_GAUSSIAN_BLUR => {
            let sigma = (param as f32 / 10.0).clamp(0.3, 10.0);
            DynamicImage::ImageLuma8(gaussian_blur(&image.to_luma8(), sigma))
        }
        OP_ROTATE_180 => image.rotate180(),
        other => return Err(err(format!("unknown operation: {other}"))),
    };
    encode(&processed, FMT_PNG, 90)
}

/// Real Brotli compression.
pub fn brotli_compress(data: &[u8], quality: u32, lgwin: u32) -> Result<Vec<u8>, CoreError> {
    let level = quality.min(11);
    let window = lgwin.clamp(10, 24);
    let mut out = Vec::with_capacity(data.len() / 3 + 64);
    {
        let mut writer = brotli::CompressorWriter::new(&mut out, 8192, level, window);
        writer.write_all(data).map_err(|e| err(format!("brotli write failed: {e}")))?;
        writer.flush().map_err(|e| err(format!("brotli flush failed: {e}")))?;
    }
    if out.len() < 2 {
        return Err(err("brotli produced no output"));
    }
    Ok(out)
}

/// Real Brotli decompression (lossless round-trip with `brotli_compress`).
pub fn brotli_decompress(data: &[u8]) -> Result<Vec<u8>, CoreError> {
    let mut out = Vec::new();
    let reader = brotli::Decompressor::new(data, 8192);
    reader.take(MAX_DECOMPRESSED_BYTES as u64 + 1).read_to_end(&mut out).map_err(|e| err(format!("brotli read failed: {e}")))?;
    if out.len() > MAX_DECOMPRESSED_BYTES { return Err(err("brotli decompressed output exceeds 64 MiB")); }
    Ok(out)
}

pub const MAX_DECOMPRESSED_BYTES: usize = 64 * 1024 * 1024;

/// Real parallel batch: work is distributed across the Rayon thread pool.
pub fn batch_process(inputs: &[Vec<u8>], op: u32, param: u32) -> Vec<Result<Vec<u8>, String>> {
    use rayon::prelude::*;
    inputs
        .par_iter()
        .map(|data| process(data, op, param).map_err(|e| e.to_string()))
        .collect()
}
/// A real end-to-end smoke test used by the CLI and by the .NET interop.
/// It builds a genuine image in memory, runs every operation, and verifies the
/// Brotli round-trip is byte-identical. Any failure is reported as an error.
pub fn self_test() -> Result<String, CoreError> {
    let width = 32_u32;
    let height = 32_u32;
    let mut pixels = Vec::with_capacity((width * height) as usize);
    for y in 0..height {
        for x in 0..width {
            pixels.push(((x.wrapping_mul(7)) ^ (y.wrapping_mul(13))) as u8);
        }
    }
    let source = GrayImage::from_raw(width, height, pixels).ok_or_else(|| err("self-test image build failed"))?;
    let png = encode(&DynamicImage::ImageLuma8(source), FMT_PNG, 90)?;
    let mut report = String::new();
    for (name, op, param) in [
        ("grayscale", OP_GRAYSCALE, 0),
        ("sobel", OP_SOBEL, 0),
        ("sharpen", OP_SHARPEN, 20),
        ("resize_half", OP_RESIZE_HALF, 0),
        ("gaussian_blur", OP_GAUSSIAN_BLUR, 30),
        ("rotate_180", OP_ROTATE_180, 0),
    ] {
        let output = process(&png, op, param)?;
        let decoded = decode(&output)?;
        if decoded.width() == 0 || decoded.height() == 0 {
            return Err(err(format!("self-test {name} produced an empty image")));
        }
        report.push_str(&format!("{name}={}B ", output.len()));
    }
    let compressed = brotli_compress(&png, 9, 22)?;
    let restored = brotli_decompress(&compressed)?;
    if restored != png {
        return Err(err("self-test brotli round-trip mismatch"));
    }
    report.push_str(&format!("brotli={}B->{}B ", png.len(), compressed.len()));
    let batch = batch_process(&[png.clone(), png.clone(), png.clone(), png], OP_SOBEL, 0);
    if batch.iter().any(|item| item.is_err()) {
        return Err(err("self-test parallel batch failed"));
    }
    report.push_str(&format!("batch={} items", batch.len()));
    Ok(report)
}
