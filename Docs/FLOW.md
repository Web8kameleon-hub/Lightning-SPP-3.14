# Rrjedha e referencës — dokument → fusha → rregulla → raport → shqyrtim

Rrjedha që duhet demonstruar para një klienti institucional. Dallimi nga një
demonstrim është se **çdo vlerë ka referencë** (dokument, faqe, pozicion, rregull,
shqyrtues) dhe **çdo hap ka provë të ekzekutuar**.

## Zinxhiri

```
burim (file | folder | watch-folder | UNC | http-upload | scanner)
  ↓ IntakeEnumerator + ScanProviderRegistry
PDF me shtresë teksti (ose PDF i skanuar, kur OCR është aktiv)
  ↓ OcrTextReader → TextLayerExtractor (pdftotext) / TesseractOcrEngine
tekst për faqe + burimi i tekstit (`text-layer` / `ocr`)
  ↓ RuleSet (JSON i operatorit) + FieldExtractor
vlera + faqe + offset
  ↓ ProvenanceTrail (JSONL i lidhur me hash)
raport + shqyrtim i emërtuar
  ↓ BatchRunner (manifest + checkpoint atomik)
rikuperim i verifikuar + paketim determinist
```

## Komandat

```bash
wwwmmm flow sources                 # çka është e disponueshme në këtë host

# rules.json
# { "version": 1, "rules": [
#   { "id": "total", "field": "invoice_total", "pattern": "Total:\\s*([0-9]+\\.[0-9]{2})", "required": true },
#   { "id": "date",  "field": "invoice_date", "pattern": "Date:\\s*(\\d{4}-\\d{2}-\\d{2})",  "required": true } ] }

wwwmmm flow extract invoice.pdf rules.json trail.jsonl
wwwmmm flow run    ./inbox rules.json ./workspace --limit 100
wwwmmm flow resume ./inbox rules.json ./workspace
wwwmmm flow verify ./workspace        # integriteti i burimeve + zinxhiri i provës
wwwmmm flow report ./workspace        # report.md me referenca për çdo fushë
wwwmmm flow review trail.jsonl <docSha256> total invoice_total 1299.00 2 0 reviewer@example.com accept
wwwmmm flow package out.zip a.pdf b.pdf
wwwmmm flow package-verify out.zip
wwwmmm flow trail-verify trail.jsonl
```

## Çka garantohet

| Vetia | Si garantohet | Prova |
| --- | --- | --- |
| Çdo vlerë ka dokument, faqe, offset | `FieldMatch` mban `Page` dhe `Offset`; rregulli regjistrohet me `RuleId` | `FlowExtractionTests.Extractor_ReturnsValueWithPageAndOffset` |
| Historiku nuk ndryshohet pa u vënë re | zinxhir hash: çdo rekord mban hash-in e të mëparshmit | `Trail_ChainsRecordsAndDetectsTampering` |
| Ndërprerja nuk humb punë | manifest + checkpoint shkruhen atomikisht pas çdo dokumenti | `Batch_InterruptedRunResumesWithoutReprocessing` |
| Një dokument i dëmtuar nuk ndal batch-in | dështimi regjistrohet, run-i vazhdon | `Batch_FailingDocumentDoesNotStopTheRun` |
| Integriteti i burimit verifikohet | SHA-256 rikalkulohet dhe krahasohet | `Batch_VerifyDetectsModifiedSource` |
| Paketa është e riprodhueshme | memberë të renditur, kohë fikse, manifest me hash | `Package_IsReproducibleAndVerifiable` |
| Ngacmimi i paketës zbulohet | verifikim hash për çdo member | `Package_VerifyDetectsModifiedMember` |
| Shtrirja e tekstit lexohet realisht | `pdftotext -layout -enc UTF-8` mbi PDF me xref të saktë | `FlowPdfTextTests.TextLayer_ReadsRealPdfPagesAndFeedsRules` |
| OCR lexon një faqe të rasterizuar | Tesseract mbi PNG-n e renderuar nga `PdfDocumentService`; rezultati kthehet pa pastrim | `OcrTests.Reader_WithOcrEnabled_RecognizesARealRasterPage`, `OcrTests.Engine_RecognizesTextFromAnImageItCannotCheat` |
| Një PDF digjital nuk degradohet nga OCR | OCR thirret vetëm kur shtresa e tekstit është bosh | `OcrTests.Reader_WithOcrDisabled_LeavesAScannedPdfEmptyAndNeverCallsTheEngine` |
| Raporti thotë kush e lexoi tekstin | `BatchEntry.textSource` shkruhet në manifest; `flow report` e shfaq | `FlowBatch.Report` |

## Kufij të deklaruar

- **OCR është opt-in dhe i kushtëzuar.** Pa `LSPP_OCR_ENABLED=true`, një PDF i
  skanuar kthehet me shtresë teksti bosh ("No text layer"), jo si dokument me
  vlera. Me flamurin aktiv dhe Tesseract të pranishëm, faqet rasterizohen dhe
  lexohen nga `TesseractOcrEngine`; pa Tesseract dështon me gabim të qartë, nuk
  kthehet bosh. Input imazh (png/jpg/tiff) ende refuzohet nga `flow run` — ktheje
  në PDF me `wwwmmm document pdf-create`. Saktësia e OCR-së nuk është matur në
  corpus real: prova ekzistuese është një imazh i vizatuar nga vetë testi.
- **Skanimi fizik nuk është provuar.** `flow sources` raporton SANE si të
  disponueshëm vetëm kur `scanimage` ekziston (Linux); WIA njihet në Windows por
  marrja e figurës nuk është implementuar; TWAIN raportohet si i padisponueshëm.
  Asnjë provider nuk deklarohet "funksional" pa një pajisje reale.
- **Kapaciteti 30 000 dokumente nuk është matur.** Rikuperimi provohet në 5
  dokumente; mekanizmat (manifest, checkpoint, hash) janë të pavarur nga numri,
  por matja e kohës dhe hapësirës në shkallë të plotë mbetet për t'u bërë.
- **Shqyrtimi regjistrohet, nuk imponohet.** `flow review` kërkon shqyrtues të
  emërtuar dhe vendim; politika se kush shqyrton çfarë mbetet jashtë kodit.
- **Email-i dhe integrimet bankare mbeten të paprovuara** (shih
  `Docs/COMPLIANCE/GAP_ANALYSIS.md`).

## Lidhja me portën e deploy-it

`Scripts/preflight-policy.py` verifikon që dokumentet dhe testet ekzistojnë, që
pretendimet e ndaluara nuk rikthehen dhe që `git diff --check` është i pastër.
`Scripts/gate-and-deploy.sh` ekzekuton politikën, build-in Release dhe suite-in e
plotë, dhe vetëm pastaj thërret `deploy/production/deploy.sh` me SHA-n e commit-it.
Pa këto hapa, deploy-i nuk fillon.
