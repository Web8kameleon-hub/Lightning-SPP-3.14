# Protokolli i testimit — regjistro gjendjen, kthehu me të dhëna reale

Qëllimi: çdo test të lërë një provë të lexueshme, dhe asnjë rezultat të mos
mbështetet në kujtesë. Komandat më poshtë ekzistojnë në këtë repo; asnjë hap nuk
kërkon shërbim që nuk është i instaluar.

## 0. Regjistro mjedisin një herë

```bash
dotnet build wwwmmm.csproj -c Release
dotnet bin/Release/net10.0/wwwmmm.dll flow doctor doctor.json
```

`doctor.json` përmban: sistemin, runtime-in, printerët që ekzistojnë vërtet
(CUPS në Linux, spooler-i në Windows), providerët e skanimit (SANE/WIA/TWAIN),
dhe a gjenden `pdftotext` dhe `java`. Nëse `printers` është bosh, `printerNote`
thotë pse — nuk është "sukses i heshtur".

## 1. Suite-i i plotë

```bash
dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj -c Release \
  --logger "trx;LogFileName=run.trx" --results-directory ./artifacts/test-run
```

Ruaj numrat e saktë: `Passed`, `Failed`, `Skipped`, `Total`. 15 skips janë
native-document dhe integrimet upstream të izoluara; nëse numri ndryshon, thuaje.

## 2. Rrjedha e dokumentit (fixtures reale)

```bash
wwwmmm flow sources
wwwmmm flow extract <dokument.pdf> rules.json trail.jsonl
wwwmmm flow run    <dosje> rules.json <workspace> --limit 10
wwwmmm flow resume <dosje> rules.json <workspace>
wwwmmm flow verify <workspace>
wwwmmm flow report <workspace>
```

Rregjistro për secilin hap: numrin e faqeve, fushave, `missingRequired`, dhe
kodardin e daljes (`$LASTEXITCODE` / `$?`). `verify` duhet të japë
`manifestValid=true` dhe `trailValid=true`; çdo `false` është gjetje, jo dështim
i testit.

## 3. Ngacmimi (duhet të dështojë)

```bash
# 1. ndrysho një bajt të një dokumenti të përpunuar
# 2. pritej: flow verify → manifestValid=false, me "hash mismatch"
# 3. ndrysho një rresht të trail.jsonl
# 4. pritej: flow trail-verify → valid=false
```

Nëse ndonjëra kthese `true` pas ndryshimit, kjo është defekt sigurie — raportoje
me skedarët e ruajtur.

## 4. Skanimi dhe printimi fizik

Këto **nuk** provohen nga suite: kërkojnë pajisje.

| Hapi | Komanda | Çka pritet |
| --- | --- | --- |
| Printerët | `flow doctor` | lista reale ose arsyeja e mungesës |
| Skanerët | `flow sources` | `sane` available vetëm me `scanimage` |
| Skanim real (Linux) | `wwwmmm device scan <device> ...` | PNG real ose gabim i qartë |
| Printim real (Linux) | `wwwmmm device print <printer> <file.pdf>` | ID pune CUPS ose gabim |

Në Windows, printimi fizik nuk është implementuar — `device` hedh
`PlatformNotSupportedException`. Kjo është kufi i deklaruar, jo defekt.

## 5. Formati i kthimit

Për secilin test, plotëso:

```
TEST:        [emri]
DATA:        [ISO 8601]
KOMANDA:     [komanda e plotë]
KOHA:        [ms ose s]
STATUS:      PASS / FAIL / BLOCKED
DALJA:       [linjat kryesore, kopjo]
ARSYEJA:     [vetëm nëse FAIL/BLOCKED]
```

Bashkangjit: `doctor.json`, `run.trx`, `manifest.jsonl`, `provenance/trail.jsonl`,
dhe `report.md`. Nëse dokumentet janë konfidenciale, dërgo vetëm hash-et dhe
numrat — trail-i mban `documentSha256`, jo përmbajtjen.

## 6. Çka do të bëjmë me të dhënat

| Rezultati | Veprimi |
| --- | --- |
| `manifestValid=false` në një dokument të paprekur | defekt në hash/lexim — rregullohet para çdo gjëje |
| `trailValid=false` pa ndryshim njerëzor | defekt në zinxhir — bllokon pranimin |
| `resume` ripërpunon dokumente të kryera | defekt në checkpoint — rregullohet |
| `missingRequired` për fusha që duhet të ekzistojnë | rregull i gabuar ose tekst që mungon — verifikohet me faqen |
| `printers=[]` në një host që ka printer | defekt në probe — raportohet me `printerNote` |
| OCR i nevojshëm për input imazh | jashtë fushës: dështon qartë, nuk shtiret |

Rregulli: asnjë rezultat nuk raportohet si "kaloi" pa komandën dhe daljen e saj.
