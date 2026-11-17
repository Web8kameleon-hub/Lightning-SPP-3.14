//! Real native core for Lightning SPP: image operations, Brotli, parallel batch.
//!
//! Every public function performs actual work on actual bytes. There is no mock branch,
//! no stub, and no fallback that returns fabricated data: if an operation cannot be
//! performed the caller gets an error, never a plausible-looking placeholder.

pub mod ffi;
pub mod ops;

pub const VERSION: &str = env!("CARGO_PKG_VERSION");
