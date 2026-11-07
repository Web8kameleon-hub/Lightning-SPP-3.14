# Ocean Battalion

An information-intake layer for Ocean Core: it collects declared open sources,
extracts their text (including scanned PDFs via OCR), splits that text into
chunks sized to the embedding model, embeds it and stores it where Ocean Core
can query it.

It is a battalion, not a super-reader. Every source is named in `sources.yaml`,
rate limited, checked against `robots.txt`, and its licence recorded. Nothing
here reaches into Google's or Microsoft's databases; those are used only through
their official APIs, with your own credentials.

---

## What it actually does

```
sources.yaml
   │
   ├─ rss         feeds            → entries
   ├─ web         one site         → HTML pages + PDFs (breadth-first, same host)
   ├─ opendata    CKAN catalogue   → CSV / JSON / XML / PDF resources
   ├─ api_google  Custom Search    → snippets + links (10 per query, official API)
   └─ api_graph   Microsoft Graph  → files the signed-in identity may read
                    │
                    ▼
              extract  ─→ html | text-layer | ocr
                    ▼
              chunk    ─→ target 4096 tokens, hard max 8192, 400 overlap
                    ▼
              embed    ─→ BAAI/bge-m3 (1024-d) or the offline hashing stub
                    ▼
              store    ─→ JSONL (default) or Chroma
```

Every stored chunk carries `source`, `url`, `reader`, `license`, `tokens`,
`embedder` and `semantic`. A record that says `reader=ocr` is telling you its
text is a guess; `reader=text-layer` is reproducible from the PDF's own bytes.

## What it does not do

- **It cannot read "everything".** Google and Microsoft have no bulk door. The
  Google agent uses the Custom Search JSON API (10 results per query, snippets
  only, no SERP scraping). The Graph agent reads one drive folder you authorise.
- **It does not decide licensing for you.** `license: unknown` is recorded as
  unknown. `Policy.licence_cleared` is the check you write against.
- **It is not a semantic search engine with the default test embedder.** The
  `hashing(offline)` embedder exists so the wiring can be tested with no 2.3 GB
  download. It has no language understanding, and `run` prints that out loud.
- **OCR quality is unmeasured.** The extraction path is real (Poppler +
  Tesseract), but no corpus of scanned Albanian documents has been benchmarked
  here. Treat `reader=ocr` output as needing review.
- **Rate limits are per source, not global.** Two sources pointed at the same
  host will each honour their own pace.

## Requirements

- Python 3.11+
- `pdftotext` and `pdftoppm` (Poppler) for PDF text and page rendering
- `tesseract` for OCR, plus the language data (`sqi` for Albanian)
- Optional: `FlagEmbedding` + `torch` for real embeddings, `chromadb` for Chroma

The tools are found in this order, and a configured path that does not exist is
an error rather than a silent fallback:

1. `OCEAN_PDFTOTEXT_PATH`, `OCEAN_PDFTOPPM_PATH`, `OCEAN_TESSERACT_PATH`
2. `PATH`
3. the standard Windows install directory (`C:\Program Files\Tesseract-OCR`)
4. a bundled copy under `../artifacts/tooling`

## Install

```bash
cd ocean-battalion
pip install -e .                 # httpx, PyYAML, beautifulsoup4, feedparser
pip install -e ".[embed]"        # optional: real embeddings
pip install -e ".[chroma]"       # optional: Chroma store
```

## Run

```bash
# what is installed and what is configured
python -m battalion.cli doctor

# prove the wiring with no network and no model download
python scripts/offline_demo.py

# the real thing (needs a populated sources.yaml)
python -m battalion.cli run --sources sources.yaml

# the same, without the model download
python -m battalion.cli run --offline --data ./data/chunks.jsonl

# what did it collect?
python -m battalion.cli stats
python -m battalion.cli query "buxheti i arsimit" --offline
```

`run` accepts `--target`, `--hard-max`, `--overlap`, `--only id1,id2` and
`--json`.

## Chunk sizing

The brief was "as much text per chunk as possible, but never more than the model
accepts". That is three numbers, and they are validated against each other:

| Setting | Default | Meaning |
| --- | --- | --- |
| `target` | 4096 | what we aim for — half of BGE-M3's window |
| `hard_max` | 8192 | the model's ceiling; nothing may exceed it |
| `overlap` | 400 | ~10 %, repeated at the head of the next chunk |

Splitting is structural first (paragraph → line → sentence → clause → word) and
destructive only when one unbroken run of text is already larger than
`hard_max`. `ChunkPlan.over_limit` is empty on success; the tests assert it.

Token counting uses the model tokenizer when `transformers` is installed and an
explicit 4-chars-per-token estimate otherwise. `ChunkPlan.counter_approximate`
says which one ran, so a report never implies tokenizer precision it did not
have.

## Source registry

```yaml
defaults:
  respect_robots: true
  rate_limit: "0.5/s"
  license: "unknown"

sources:
  - id: alb-opendata
    type: opendata
    endpoint: "https://opendata.gov.al"
    ocr: true
```

Configuration is validated at load time: unknown types, duplicate ids, missing
per-type keys and unparseable rate limits all fail before anything is fetched.
`tests/test_policy_and_config.py` pins that behaviour.

## Testing

```bash
cd ocean-battalion
python -m pytest -q
```

37 tests, no network. The PDF tests use a hand-built PDF with a real xref table
and run against the host's Poppler; the OCR branch is exercised with an injected
subprocess runner so it is covered even where Tesseract is absent.

## Layout

```
battalion/
  config.py        sources.yaml → validated Config
  policy.py        robots.txt, rate limiting, licence gating
  fetch.py         Fetcher protocol + HttpFetcher + RecordedFetcher
  orchestrator.py  builds and runs one agent per source
  cli.py           doctor | run | stats | query
  workers/         rss, web, opendata, api_google, api_graph
  pipeline/        tokens, locator, extract, chunk, embed, ingest
  storage/         base contract, JSONL store, Chroma adapter
scripts/           offline_demo.py
tests/             37 tests, offline
```

## Where this belongs

This is a standalone Python project inside the Lightning SPP repository. It
shares the OCR discipline of the .NET engine (`Documents/OcrTextReader.cs`) —
text layer first, OCR only when the layer is empty, and always report which
reader produced the text — but it shares no code and no runtime with it.
