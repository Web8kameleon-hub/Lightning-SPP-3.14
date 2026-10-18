# Install the Lightning SPP runtime

Runtime archives contain the application, .NET runtime, static web files and
license notices. They do not contain activation keys, provider credentials,
customer records, configured AI models or a running Longo persistence service.
These are portable runtime packages, not signed MSI installers.

## Windows x64

Verify the ZIP against its accompanying SHA-256 file using `Get-FileHash`.
Extract it to a new directory, open PowerShell in that directory, then run:

```powershell
.\wwwmmm.exe --help
.\wwwmmm.exe demo
.\wwwmmm.exe document pdf-create example.pdf Web/wwwroot/favicon-32x32.png
```

The first two commands and PDF creation do not require Poppler. PDF rendering
and PDF demo processing require separately installed trusted `pdfinfo` and
`pdftoppm` executables. Set their absolute paths in `SPP_PDFINFO_PATH` and
`SPP_PDFTOPPM_PATH` when they are not in PATH. Windows device scanning and
physical printing are not established by this package.

## Linux x64

Use a glibc-based distribution supported by .NET 10. The validation workflow
runs on Ubuntu; this archive is not an Alpine/musl build.

```bash
sha256sum -c lightning-spp-3.14.1-linux-x64.tar.gz.sha256
tar -xzf lightning-spp-3.14.1-linux-x64.tar.gz
cd lightning-spp-3.14.1-linux-x64
./wwwmmm --help
./wwwmmm demo
```

For PDF rendering on Ubuntu, install `poppler-utils` and `fonts-dejavu-core`.
CUPS/SANE adapters additionally require `cups-client`/`sane-utils`, accessible
hardware and a separately validated device configuration. A queue submission
does not establish successful physical printing.

## Local document measurements

```powershell
.\wwwmmm.exe benchmark-documents C:\documents C:\reports\new-report.json 3
```

On Linux use `./wwwmmm` with Linux paths. The input directory is read locally,
non-recursively; supported files are not uploaded or modified. The destination
directory must already exist. A nonzero exit code reports rejected inputs or
failed processing; inspect the JSON report. Keep the report private if hashes
or document sizes are sensitive. This command measures the grayscale demo
pipeline, not AI inference, OCR accuracy or clinical suitability.

## Hosting and activation

**Standalone commercial activation is not established by these archives.** The
current local validator uses issuer HMAC signing material. Never give an issuer's
signing secret to a customer device to make activation pass. A secure standalone
commercial distribution requires public-key verification or an authoritative
online activation flow, including reviewed migration of existing keys. See the
[security review](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/blob/main/Docs/ACCOUNT_SECURITY_REVIEW.md).

Production hosting requires private environment configuration, HTTPS, durable
Longo state, data-protection keys and verified email-provider configuration.
Follow [the production deployment guide](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/blob/main/deploy/production/README.md).
Do not expose an unconfigured local instance as a customer service.

Existing customers retrieve their existing key from their verified account.
Re-registering does not purchase or issue another license. Use a configured
issuer-managed service for the current hosted activation flow; never place
keys or issuer secrets in shared archives.
The component license notices shipped in the archive apply.

## Upgrade and recovery

Stop the old process and extract each version into a new directory. Preserve
the old runtime directory, private environment, customer data and Longo backups.
Verify `--help`, a representative document and hosted health before switching
traffic. Revert the executable directory to recover from an application failure;
do not erase or roll back customer data without a reviewed recovery procedure.

## Building archives

With the private build license loaded, run:

```powershell
python Scripts/package-release.py --runtime win-x64
python Scripts/package-release.py --runtime linux-x64
```

Output is under `artifacts/distribution`. Reusing a staging directory is refused.
Each archive has a SHA-256 sidecar and an internal file-hash manifest. A manifest
is an integrity record, not a publisher signature or evidence of customer acceptance.
