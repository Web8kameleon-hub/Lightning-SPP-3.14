use image::{DynamicImage, GrayImage};
use lightning_core::ops::{
    self, brotli_compress, brotli_decompress, encode, CoreError, FMT_BMP, FMT_JPEG, FMT_PNG,
    FMT_TIFF, OP_GAUSSIAN_BLUR, OP_GRAYSCALE, OP_RESIZE_HALF, OP_ROTATE_180, OP_SHARPEN, OP_SOBEL,
};

fn make_png(width: u32, height: u32) -> Vec<u8> {
    let mut pixels = Vec::with_capacity((width * height) as usize);
    for y in 0..height {
        for x in 0..width {
            pixels.push((x.wrapping_mul(17) ^ y.wrapping_mul(31)) as u8);
        }
    }
    let image = GrayImage::from_raw(width, height, pixels).expect("image buffer");
    encode(&DynamicImage::ImageLuma8(image), FMT_PNG, 90).expect("png encode")
}

#[test]
fn decode_reads_real_dimensions() {
    let image = ops::decode(&make_png(64, 48)).expect("decode");
    assert_eq!((image.width(), image.height()), (64, 48));
}

#[test]
fn decompression_rejects_expansion_beyond_resource_budget() {
    let input = vec![0u8; ops::MAX_DECOMPRESSED_BYTES + 1];
    let compressed = brotli_compress(&input, 0, 18).expect("fixture compression");
    assert!(brotli_decompress(&compressed).is_err());
}

#[test]
fn sobel_preserves_size_and_emits_bytes() {
    let output = ops::process(&make_png(64, 64), OP_SOBEL, 0).expect("sobel");
    assert!(output.len() > 8);
    let decoded = ops::decode(&output).expect("decode sobel");
    assert_eq!((decoded.width(), decoded.height()), (64, 64));
}

#[test]
fn sharpen_actually_changes_pixels() {
    let png = make_png(48, 48);
    let base = ops::decode(&png).expect("decode").to_luma8().into_raw();
    let sharpened = ops::process(&png, OP_SHARPEN, 30).expect("sharpen");
    let after = ops::decode(&sharpened)
        .expect("decode")
        .to_luma8()
        .into_raw();
    assert_ne!(base, after, "sharpen returned identical pixels");
}

#[test]
fn resize_half_halves_dimensions() {
    let output = ops::process(&make_png(40, 40), OP_RESIZE_HALF, 0).expect("resize");
    let decoded = ops::decode(&output).expect("decode");
    assert_eq!((decoded.width(), decoded.height()), (20, 20));
}

#[test]
fn grayscale_drops_colour_channels() {
    let mut raw = Vec::new();
    for y in 0..16_u8 {
        for x in 0..16_u8 {
            raw.extend_from_slice(&[x * 16, y * 16, 128]);
        }
    }
    let rgb = image::RgbImage::from_raw(16, 16, raw).expect("rgb buffer");
    let png = encode(&DynamicImage::ImageRgb8(rgb), FMT_PNG, 90).expect("encode");
    let output = ops::process(&png, OP_GRAYSCALE, 0).expect("grayscale");
    assert_eq!(
        ops::decode(&output).expect("decode").color(),
        image::ColorType::L8
    );
}

#[test]
fn gaussian_blur_reduces_variance() {
    let png = make_png(64, 64);
    let base = ops::decode(&png).expect("decode").to_luma8().into_raw();
    let blurred = ops::process(&png, OP_GAUSSIAN_BLUR, 20).expect("blur");
    let after = ops::decode(&blurred).expect("decode").to_luma8().into_raw();
    let variance = |values: &[u8]| -> f64 {
        let mean = values.iter().map(|&v| f64::from(v)).sum::<f64>() / values.len() as f64;
        values
            .iter()
            .map(|&v| (f64::from(v) - mean).powi(2))
            .sum::<f64>()
            / values.len() as f64
    };
    assert!(
        variance(&after) < variance(&base),
        "blur did not smooth the image"
    );
}

#[test]
fn rotate_180_is_its_own_inverse() {
    let png = make_png(24, 18);
    let once = ops::process(&png, OP_ROTATE_180, 0).expect("rotate");
    let twice = ops::process(&once, OP_ROTATE_180, 0).expect("rotate twice");
    let original = ops::decode(&png).expect("decode").to_luma8().into_raw();
    let restored = ops::decode(&twice).expect("decode").to_luma8().into_raw();
    assert_eq!(original, restored);
}

#[test]
fn brotli_round_trip_is_byte_identical() {
    let png = make_png(128, 128);
    let compressed = brotli_compress(&png, 9, 22).expect("compress");
    assert!(!compressed.is_empty());
    assert_eq!(brotli_decompress(&compressed).expect("decompress"), png);
}

#[test]
fn jpeg_quality_is_clamped_and_marked() {
    let image = ops::decode(&make_png(32, 32)).expect("decode");
    let jpeg = encode(&image, FMT_JPEG, 500).expect("jpeg encode");
    assert_eq!(&jpeg[0..2], &[0xFF, 0xD8]);
    assert_eq!(&jpeg[jpeg.len() - 2..], &[0xFF, 0xD9]);
}

#[test]
fn bmp_and_tiff_encoders_emit_their_own_formats() {
    let image = ops::decode(&make_png(16, 16)).expect("decode");
    let bmp = encode(&image, FMT_BMP, 0).expect("bmp");
    let tiff = encode(&image, FMT_TIFF, 0).expect("tiff");
    assert_eq!(&bmp[0..2], b"BM");
    assert!(
        tiff.starts_with(b"II") || tiff.starts_with(b"MM"),
        "missing TIFF magic"
    );
}

#[test]
fn parallel_batch_processes_every_item() {
    let png = make_png(32, 32);
    let inputs = vec![png.clone(), png.clone(), png.clone(), png];
    let results = ops::batch_process(&inputs, OP_SOBEL, 0);
    assert_eq!(results.len(), 4);
    assert!(results.iter().all(|r| r.is_ok()));
}

#[test]
fn unknown_operation_is_an_error_not_a_silent_pass() {
    let result = ops::process(&make_png(8, 8), 999, 0);
    assert!(matches!(result, Err(CoreError(_))));
}

#[test]
fn corrupt_input_is_an_error_not_a_panic() {
    assert!(ops::process(b"not an image at all", OP_SOBEL, 0).is_err());
    assert!(ops::decode(b"").is_err());
}

#[test]
fn self_test_reports_every_stage() {
    let report = ops::self_test().expect("self-test");
    assert!(report.contains("sobel="), "missing sobel stage: {report}");
    assert!(report.contains("brotli="), "missing brotli stage: {report}");
    assert!(report.contains("batch=4"), "missing batch stage: {report}");
}
