//! C ABI for .NET 10 (LibraryImport) and any other host.
//!
//! Contract:
//! * Every function returns 0 on success or a negative error code.
//! * Output buffers are allocated with `libc::malloc` and MUST be released with
//!   `lightning_free`. The host never frees with a different allocator.
//! * No panic may cross this boundary: every entry point wraps its work in
//!   `catch_unwind` and maps a panic to `ERR_PANIC`.

use crate::{ops, VERSION};
use libc::{c_char, c_int};
use std::ffi::CString;
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::ptr;
use std::sync::OnceLock;

pub const OK: c_int = 0;
pub const ERR_NULL: c_int = -1;
pub const ERR_OOM: c_int = -2;
pub const ERR_PANIC: c_int = -3;
pub const ERR_OPERATION: c_int = -4;
pub const ERR_ENCODING: c_int = -5;

static VERSION_CSTRING: OnceLock<CString> = OnceLock::new();

fn write_out(source: &[u8], out: *mut *mut u8, out_len: *mut usize) -> c_int {
    unsafe {
        if out.is_null() || out_len.is_null() {
            return ERR_NULL;
        }
        let len = source.len();
        let buffer = libc::malloc(len.max(1)) as *mut u8;
        if buffer.is_null() {
            return ERR_OOM;
        }
        if len > 0 {
            ptr::copy_nonoverlapping(source.as_ptr(), buffer, len);
        }
        *out = buffer;
        *out_len = len;
    }
    OK
}

/// Library version as a NUL-terminated UTF-8 string. Valid for the process lifetime;
/// the caller must not free it.
#[no_mangle]
pub extern "C" fn lightning_version() -> *const c_char {
    VERSION_CSTRING
        .get_or_init(|| CString::new(VERSION).unwrap_or_else(|_| CString::new("unknown").unwrap()))
        .as_ptr()
}

/// Frees a buffer previously produced by this library.
#[no_mangle]
pub unsafe extern "C" fn lightning_free(buffer: *mut u8) {
    if !buffer.is_null() {
        libc::free(buffer as *mut libc::c_void);
    }
}

/// Applies one operation and returns PNG bytes.
#[no_mangle]
pub unsafe extern "C" fn lightning_process(
    input: *const u8,
    input_len: usize,
    op: u32,
    param: u32,
    out: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    if input.is_null() || out.is_null() || out_len.is_null() {
        return ERR_NULL;
    }
    let slice = std::slice::from_raw_parts(input, input_len);
    match catch_unwind(AssertUnwindSafe(|| ops::process(slice, op, param))) {
        Ok(Ok(bytes)) => write_out(&bytes, out, out_len),
        Ok(Err(_)) => ERR_OPERATION,
        Err(_) => ERR_PANIC,
    }
}

/// Re-encodes input into the requested format.
#[no_mangle]
pub unsafe extern "C" fn lightning_encode(
    input: *const u8,
    input_len: usize,
    format: u32,
    quality: u32,
    out: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    if input.is_null() || out.is_null() || out_len.is_null() {
        return ERR_NULL;
    }
    let slice = std::slice::from_raw_parts(input, input_len);
    let result = catch_unwind(AssertUnwindSafe(|| {
        ops::decode(slice).and_then(|image| ops::encode(&image, format, quality))
    }));
    match result {
        Ok(Ok(bytes)) => write_out(&bytes, out, out_len),
        Ok(Err(_)) => ERR_ENCODING,
        Err(_) => ERR_PANIC,
    }
}

#[no_mangle]
pub unsafe extern "C" fn lightning_brotli_compress(
    input: *const u8,
    input_len: usize,
    quality: u32,
    lgwin: u32,
    out: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    if input.is_null() || out.is_null() || out_len.is_null() {
        return ERR_NULL;
    }
    let slice = std::slice::from_raw_parts(input, input_len);
    match catch_unwind(AssertUnwindSafe(|| {
        ops::brotli_compress(slice, quality, lgwin)
    })) {
        Ok(Ok(bytes)) => write_out(&bytes, out, out_len),
        Ok(Err(_)) => ERR_OPERATION,
        Err(_) => ERR_PANIC,
    }
}

#[no_mangle]
pub unsafe extern "C" fn lightning_brotli_decompress(
    input: *const u8,
    input_len: usize,
    out: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    if input.is_null() || out.is_null() || out_len.is_null() {
        return ERR_NULL;
    }
    let slice = std::slice::from_raw_parts(input, input_len);
    match catch_unwind(AssertUnwindSafe(|| ops::brotli_decompress(slice))) {
        Ok(Ok(bytes)) => write_out(&bytes, out, out_len),
        Ok(Err(_)) => ERR_OPERATION,
        Err(_) => ERR_PANIC,
    }
}

/// Runs the real self-test. Returns 0 when every operation and the lossless
/// Brotli round-trip succeeded.
#[no_mangle]
pub extern "C" fn lightning_self_test() -> c_int {
    match catch_unwind(AssertUnwindSafe(ops::self_test)) {
        Ok(Ok(_)) => OK,
        Ok(Err(_)) => ERR_OPERATION,
        Err(_) => ERR_PANIC,
    }
}
/// Parallel batch over the Rayon pool.
///
/// `inputs[i]`/`lens[i]` describe each item; results land in `out_ptrs[i]`/`out_lens[i]`
/// and the per-item status in `codes[i]`. Every successful buffer must be released with
/// `lightning_free`, including the ones produced when the batch reports partial failure.
#[no_mangle]
pub unsafe extern "C" fn lightning_batch_process(
    inputs: *const *const u8,
    lens: *const usize,
    count: usize,
    op: u32,
    param: u32,
    out_ptrs: *mut *mut u8,
    out_lens: *mut usize,
    codes: *mut c_int,
) -> c_int {
    if inputs.is_null()
        || lens.is_null()
        || out_ptrs.is_null()
        || out_lens.is_null()
        || codes.is_null()
    {
        return ERR_NULL;
    }
    if count > 256 { return ERR_OPERATION; }
    let mut total = 0usize;
    for index in 0..count {
        *out_ptrs.add(index) = ptr::null_mut();
        *out_lens.add(index) = 0;
        *codes.add(index) = ERR_OPERATION;
        total = match total.checked_add(*lens.add(index)) {
            Some(total) if total <= 64 * 1024 * 1024 => total,
            _ => return ERR_OPERATION,
        };
    }
    let mut owned: Vec<Vec<u8>> = Vec::with_capacity(count);
    for index in 0..count {
        let pointer = *inputs.add(index);
        let length = *lens.add(index);
        if pointer.is_null() && length != 0 {
            return ERR_NULL;
        }
        owned.push(if length == 0 {
            Vec::new()
        } else {
            std::slice::from_raw_parts(pointer, length).to_vec()
        });
    }
    let results = match catch_unwind(AssertUnwindSafe(|| ops::batch_process(&owned, op, param))) {
        Ok(results) => results,
        Err(_) => return ERR_PANIC,
    };
    for (index, item) in results.iter().enumerate() {
        match item {
            Ok(bytes) => {
                let code = write_out(bytes, out_ptrs.add(index), out_lens.add(index));
                *codes.add(index) = code;
            }
            Err(_) => {
                *out_ptrs.add(index) = ptr::null_mut();
                *out_lens.add(index) = 0;
                *codes.add(index) = ERR_OPERATION;
            }
        }
    }
    OK
}
