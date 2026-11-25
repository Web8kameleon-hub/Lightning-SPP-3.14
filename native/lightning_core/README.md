# lightning_core

Real native core for Lightning SPP: image operations, Brotli, and a parallel batch runner.
Every public function works on actual bytes — there is no mock branch, no stub and no
fallback that returns fabricated data. If an operation cannot be performed the caller gets
an error, never a plausible-looking placeholder.

- Crate: `lib` + `cdylib` (`lightning_core.dll` / `liblightning_core.so`) and the `lightning-cli` binary
- Rust edition 2021; build verified on 1.94.1. Older toolchains have not been accepted.
- Dependencies: `image` (png/jpeg/bmp/tiff), `brotli`, `rayon`, `libc`

## Build, test, run

This crate is its own Cargo workspace (`native/lightning_core`), so run cargo from here:

```powershell
cd native/lightning_core
cargo build --release          # cdylib + lightning-cli in target/release
cargo test --all-targets       # integration suite in tests/integration.rs
cargo run --release --bin lightning-cli -- self-test
```

`self-test` runs every operation and a lossless Brotli round-trip and prints a report.
Exit code is 0 only when all stages passed.

CLI operations: `gray`, `sobel`, `sharpen`, `resize`, `blur`, `rotate`, `brotli`.

```powershell
cargo run --release --bin lightning-cli -- sobel photo.png out.png
cargo run --release --bin lightning-cli -- brotli archive.bin archive.bin.br
```

To build from the repository root instead, pass the manifest explicitly:

```powershell
cargo build --release --manifest-path native/lightning_core/Cargo.toml
```

## C ABI (`src/ffi.rs`)

Consumed by .NET 10 `LibraryImport` and by any other host.

- Every function returns `0` on success or a negative error code
  (`ERR_NULL`, `ERR_OOM`, `ERR_PANIC`, `ERR_OPERATION`, `ERR_ENCODING`).
- Output buffers are allocated with `libc::malloc` and **must** be released with
  `lightning_free` — never with the host allocator.
- Brotli decompression rejects outputs above 64 MiB. Native/managed batches
  accept at most 256 items and 64 MiB of encoded input. These are resource limits,
  not measured throughput or memory SLOs.
- No panic crosses the boundary: every entry point wraps its work in `catch_unwind`
  and maps a panic to `ERR_PANIC`.
- `lightning_version` returns a static NUL-terminated string that the caller must not free.

## Editor / rust-analyzer

The repository root is not a Cargo workspace: the only Rust crate is this one, and the
rest of the repository is .NET. rust-analyzer started at the root therefore finds no
`Cargo.toml` and reports:

```
ERROR failed to find any projects in [AbsPathBuf("...Lightning-SPP-3.14")]
ERROR FetchWorkspaceError: rust-analyzer failed to fetch workspace
```

The committed `.vscode/settings.json` fixes this by declaring the crate explicitly:

```json
"rust-analyzer.linkedProjects": ["native/lightning_core/Cargo.toml"]
```

No root `Cargo.toml` is added on purpose: a root workspace would relocate the crate's
build directory (`native/lightning_core/target`) and its `Cargo.lock`, which existing
build and release steps depend on.

If you add another Rust crate, append its `Cargo.toml` to `rust-analyzer.linkedProjects`
(or introduce a real workspace and update those paths in the same change).

## Build artifacts

`target/` is ignored by `.gitignore` (`/native/lightning_core/target/`). Never commit it.
