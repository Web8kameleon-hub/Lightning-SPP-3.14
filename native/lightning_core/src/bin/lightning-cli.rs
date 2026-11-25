use lightning_core::ops::{self, FMT_JPEG, FMT_PNG};
use std::env;
use std::fs;
use std::process::ExitCode;
use std::time::Instant;

fn main() -> ExitCode {
    let args: Vec<String> = env::args().collect();
    if args.len() < 2 {
        eprintln!("usage: lightning-cli <self-test|gray|sobel|sharpen|resize|blur|rotate|brotli> <input> [output]");
        return ExitCode::from(2);
    }
    match args[1].as_str() {
        "self-test" => match ops::self_test() {
            Ok(report) => {
                println!("self-test OK: {report}");
                ExitCode::SUCCESS
            }
            Err(e) => {
                eprintln!("self-test FAILED: {e}");
                ExitCode::FAILURE
            }
        },
        "brotli" if args.len() >= 3 => {
            let data = match fs::read(&args[2]) {
                Ok(d) => d,
                Err(e) => {
                    eprintln!("read failed: {e}");
                    return ExitCode::from(1);
                }
            };
            let compressed = ops::brotli_compress(&data, 9, 22).expect("brotli compress");
            let restored = ops::brotli_decompress(&compressed).expect("brotli decompress");
            if restored != data {
                eprintln!("round-trip mismatch");
                return ExitCode::FAILURE;
            }
            let out = args
                .get(3)
                .cloned()
                .unwrap_or_else(|| format!("{}.br", args[2]));
            fs::write(&out, &compressed).expect("write");
            println!(
                "{}B -> {}B ({:.2}%), lossless round-trip verified -> {out}",
                data.len(),
                compressed.len(),
                100.0 * compressed.len() as f64 / data.len().max(1) as f64
            );
            ExitCode::SUCCESS
        }
        name if args.len() >= 3 => {
            let (op, param) = match name {
                "gray" => (ops::OP_GRAYSCALE, 0),
                "sobel" => (ops::OP_SOBEL, 0),
                "sharpen" => (ops::OP_SHARPEN, 20),
                "resize" => (ops::OP_RESIZE_HALF, 0),
                "blur" => (ops::OP_GAUSSIAN_BLUR, 30),
                "rotate" => (ops::OP_ROTATE_180, 0),
                other => {
                    eprintln!("unknown operation: {other}");
                    return ExitCode::from(2);
                }
            };
            let data = match fs::read(&args[2]) {
                Ok(d) => d,
                Err(e) => {
                    eprintln!("read failed: {e}");
                    return ExitCode::from(1);
                }
            };
            let started = Instant::now();
            let png = match ops::process(&data, op, param) {
                Ok(p) => p,
                Err(e) => {
                    eprintln!("process failed: {e}");
                    return ExitCode::FAILURE;
                }
            };
            let elapsed = started.elapsed();
            let out = args
                .get(3)
                .cloned()
                .unwrap_or_else(|| format!("{name}.out.png"));
            fs::write(&out, &png).expect("write");
            let jpeg =
                ops::encode(&ops::decode(&png).expect("decode"), FMT_JPEG, 85).expect("jpeg");
            let _ = FMT_PNG;
            println!(
                "{name}: {}B in -> {}B png ({}B jpeg) in {:.2} ms -> {out}",
                data.len(),
                png.len(),
                jpeg.len(),
                elapsed.as_secs_f64() * 1000.0
            );
            ExitCode::SUCCESS
        }
        _ => {
            eprintln!("bad arguments");
            ExitCode::from(2)
        }
    }
}
