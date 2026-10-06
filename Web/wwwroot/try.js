'use strict';
const byId = id => document.getElementById(id);
function message(id, text, kind = '') { const el = byId(id); el.textContent = text; el.className = 'message ' + kind; }
function formatBytes(bytes) {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
}

byId('tryForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  const fileInput = byId('fileInput');
  const file = fileInput.files[0];
  if (!file) { message('tryStatus', 'Choose a document or image file first.', 'error'); return; }
  if (file.size > 8 * 1024 * 1024) { message('tryStatus', 'File is larger than 8 MB. Choose a smaller file.', 'error'); return; }

  const profile = byId('profileSelect').value;
  const button = event.currentTarget.querySelector('button');
  const oldLabel = button.textContent;
  button.disabled = true; button.textContent = 'Processing…';
  message('tryStatus', file.type === 'application/pdf' ? 'PDF received. Validating and processing all pages (maximum 32)…' : 'Image received. Running the real Scan and Process pipeline…');
  byId('results').hidden = true;

  try {
    const response = await fetch('/api/try/process', {
      method: 'POST',
      headers: { 'Content-Type': file.type, 'X-Stigma-Profile': profile },
      body: file,
      signal: AbortSignal.timeout(file.type === 'application/pdf' ? 2100000 : 30000)
    });

    const data = await response.json();
    if (!response.ok || !data.ok) {
      throw new Error(data.error || 'Processing failed. Please try a different file.');
    }

    const isPdf = data.inputType === 'pdf';
    const beforeImage = byId('beforeImage');
    const beforeInfo = byId('beforeDocumentInfo');
    if (isPdf) {
      beforeImage.removeAttribute('src');
      beforeImage.hidden = true;
      beforeInfo.hidden = false;
      beforeInfo.textContent = `PDF document • all ${data.totalPages} page${data.totalPages === 1 ? '' : 's'} processed • page 1 preview`;
    } else {
      beforeImage.src = URL.createObjectURL(file);
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
    byId('metaCompressedSize').textContent = formatBytes(data.compressedBytes);
    byId('metaCompression').textContent = data.compressionPercent + '%';

    byId('results').hidden = false;
    message('tryStatus', isPdf ? `Done. All ${data.totalPages} pages were scanned and processed. Download your processed PDF below; the preview shows page 1.` : 'Done. Your image was scanned and processed successfully. Your result is ready below.', 'success');
  } catch (error) {
    message('tryStatus', error.message, 'error');
  } finally {
    button.disabled = false; button.textContent = oldLabel;
  }
});
