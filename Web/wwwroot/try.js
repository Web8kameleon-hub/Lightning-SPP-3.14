'use strict';
const byId = id => document.getElementById(id);
const pdfOnly = byId('tryForm').dataset.product === 'pdf-processor';
const pdfProfiles = (byId('tryForm').dataset.profiles || '').split(',');
let beforeUrl;
let zipUrl;
function message(id, text, kind = '') { const el = byId(id); el.textContent = text; el.className = 'message ' + kind; }
function formatBytes(bytes) {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
}

byId('tryForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  byId('zipResult').hidden = true;
  byId('zipDownload').removeAttribute('href');
  if (zipUrl) { URL.revokeObjectURL(zipUrl); zipUrl = undefined; }
  byId('results').hidden = true;
  byId('downloadLink').removeAttribute('href');
  const fileInput = byId('fileInput');
  const file = fileInput.files[0];
  if (!file) { message('tryStatus', 'Choose a document or image file first.', 'error'); return; }
  if (file.size > 8 * 1024 * 1024) { message('tryStatus', 'File is larger than 8 MB. Choose a smaller file.', 'error'); return; }
  if (pdfOnly && file.type !== 'application/pdf' && !/\.pdf$/i.test(file.name)) {
    message('tryStatus', 'Choose a PDF file for PDF Processor.', 'error'); return;
  }
  const archiveInput = !pdfOnly && /\.zip$/i.test(file.name);
  const contentType = archiveInput ? 'application/zip' : pdfOnly ? 'application/pdf' : file.type;

  const structural = archiveInput && Boolean(byId('archiveAnalysis')?.checked);
  const wantsZip = archiveInput || byId('zipPackage').checked;
  const profile = byId('profileSelect').value;
  if (pdfOnly && !pdfProfiles.includes(profile)) {
    message('tryStatus', 'This profile is not available in PDF Processor.', 'error'); return;
  }
  const button = event.currentTarget.querySelector('button');
  const oldLabel = button.textContent;
  button.disabled = true; button.textContent = 'Processing…';
  fileInput.disabled = true; byId('profileSelect').disabled = true;
  message('tryStatus', archiveInput ? 'Validating ZIP and inventorying files without executing code…' : contentType === 'application/pdf' ? 'Submitting PDF for validation and processing of all pages (maximum 32). Waiting for the server result…' : 'Submitting image to the Scan and Process pipeline. Waiting for the server result…');
  byId('results').hidden = true;
  byId('downloadLink').removeAttribute('href');

  try {
    const response = await fetch(archiveInput ? '/api/try/archive' : wantsZip ? '/api/try/package' : '/api/try/process', {
      method: 'POST',
      headers: { 'Content-Type': contentType, 'X-Stigma-Profile': profile, ...(archiveInput ? { 'X-Archive-Analysis': structural ? 'structural' : 'none' } : {}) },
      body: file,
      signal: AbortSignal.timeout(contentType === 'application/pdf' ? 2100000 : 30000)
    });

    if (wantsZip && response.ok) {
      if (!(response.headers.get('Content-Type') || '').startsWith('application/zip')) throw new Error('Unexpected package response.');
      const blob = await response.blob();
      const signature = new Uint8Array(await blob.slice(0, 4).arrayBuffer());
      if (signature.join(',') !== '80,75,3,4') throw new Error('Invalid ZIP package.');
      zipUrl = URL.createObjectURL(blob);
      byId('zipDownload').href = zipUrl;
      byId('zipResult').hidden = false;
      message('tryStatus', archiveInput ? (structural ? 'Done. Structural analysis is included in report.json. Check each file status for invalid, unsupported or size-limited files; originals are preserved.' : 'Done. Original files and inventory report are ready. Categories are based on extensions; no code was executed or content converted.') : 'Done. Your processed document and verification report are ready in one ZIP. No supplier classification was performed.', 'success');
      return;
    }
    const data = await response.json();
    if (!response.ok || !data.ok) {
      throw new Error(data.error || 'Processing failed. Please try a different file.');
    }

    const isPdf = data.inputType === 'pdf';
    if (data.profile !== profile || (pdfOnly && !isPdf)) throw new Error('The server returned an unexpected processing result.');
    if (isPdf) {
      if (data.outputMimeType !== 'application/pdf' || data.outputFileExtension !== '.pdf' ||
          !Number.isInteger(data.totalPages) || data.totalPages < 1 || data.totalPages > 32 ||
          typeof data.outputPdfBase64 !== 'string' || !atob(data.outputPdfBase64).startsWith('%PDF-') ||
          atob(data.outputPdfBase64).length !== data.afterBytes) {
        throw new Error('The processed PDF output is missing or invalid.');
      }
    }
    if (typeof data.afterImageBase64 !== 'string' || !atob(data.afterImageBase64).startsWith('\x89PNG\r\n\x1a\n')) {
      throw new Error('The processed page preview is missing or invalid.');
    }
    const beforeImage = byId('beforeImage');
    const beforeInfo = byId('beforeDocumentInfo');
    if (isPdf) {
      beforeImage.removeAttribute('src');
      beforeImage.hidden = true;
      beforeInfo.hidden = false;
      beforeInfo.textContent = `PDF document • all ${data.totalPages} page${data.totalPages === 1 ? '' : 's'} processed • page 1 preview`;
    } else {
      if (beforeUrl) URL.revokeObjectURL(beforeUrl);
      beforeImage.src = beforeUrl = URL.createObjectURL(file);
      beforeImage.hidden = false;
      beforeInfo.hidden = true;
      beforeInfo.textContent = '';
    }
    const afterSrc = 'data:image/png;base64,' + data.afterImageBase64;
    byId('afterImage').src = afterSrc;
    const download = byId('downloadLink');
    if (isPdf && (!data.outputPdfBase64 || data.outputMimeType !== 'application/pdf')) {
      throw new Error('The processed PDF output is missing or invalid.');
    }
    download.href = isPdf ? 'data:application/pdf;base64,' + data.outputPdfBase64 : afterSrc;
    const stem = file.name.replace(/\.[^.]*$/, '').replace(/[^a-zA-Z0-9_-]/g, '_').slice(0, 100) || 'document';
    download.download = stem + '-processed' + (isPdf ? '.pdf' : '.png');
    download.textContent = isPdf ? 'Download processed PDF' : 'Download processed image';

    byId('metaInput').textContent = isPdf ? `PDF • all ${data.totalPages} pages processed` : 'Image';
    byId('metaProfile').textContent = data.profile;
    byId('metaScanTime').textContent = data.scanTimeMs + ' ms';
    byId('metaProcessTime').textContent = data.processTimeMs + ' ms';
    byId('metaTotalTime').textContent = data.totalTimeMs + ' ms';
    byId('metaOriginalSize').textContent = formatBytes(data.originalBytes);
    byId('metaCompressedSize').textContent = formatBytes(data.afterBytes);
    byId('metaCompression').textContent = data.compressionPercent + '%';

    byId('results').hidden = false;
    const sizeNote = data.afterBytes > data.originalBytes ? ' The output is larger than the original.' : '';
    message('tryStatus', (isPdf ? `Done. All ${data.totalPages} pages were processed into a rasterized PDF. Text selection, vectors and forms are not preserved. The preview shows page 1.` : 'Done. Your processed image is ready below.') + sizeNote, 'success');
  } catch (error) {
    message('tryStatus', error.message, 'error');
  } finally {
    fileInput.disabled = false; byId('profileSelect').disabled = false;
    button.disabled = false; button.textContent = oldLabel;
  }
});
