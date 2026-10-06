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
  message('tryStatus', file.type === 'application/pdf' ? 'PDF received. Validating the document, rendering page 1 and running the real pipeline…' : 'Image received. Running the real Scan and Process pipeline…');
  byId('results').hidden = true;

  try {
    const response = await fetch('/api/try/process', {
      method: 'POST',
      headers: { 'Content-Type': file.type, 'X-Stigma-Profile': profile },
      body: file,
      signal: AbortSignal.timeout(30000)
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
      beforeInfo.textContent = `PDF document • ${data.totalPages} page${data.totalPages === 1 ? '' : 's'} • page ${data.page} processed`;
    } else {
      beforeImage.src = URL.createObjectURL(file);
      beforeImage.hidden = false;
      beforeInfo.hidden = true;
      beforeInfo.textContent = '';
    }
    const afterSrc = 'data:image/png;base64,' + data.afterImageBase64;
    byId('afterImage').src = afterSrc;
    byId('downloadLink').href = afterSrc;

    byId('metaInput').textContent = data.inputType === 'pdf' ? `PDF • page ${data.page} of ${data.totalPages}` : 'Image';
    byId('metaProfile').textContent = data.profile;
    byId('metaScanTime').textContent = data.scanTimeMs + ' ms';
    byId('metaProcessTime').textContent = data.processTimeMs + ' ms';
    byId('metaTotalTime').textContent = data.totalTimeMs + ' ms';
    byId('metaOriginalSize').textContent = formatBytes(data.originalBytes);
    byId('metaCompressedSize').textContent = formatBytes(data.compressedBytes);
    byId('metaCompression').textContent = data.compressionPercent + '%';

    byId('results').hidden = false;
    message('tryStatus', data.inputType === 'pdf' ? `Done. PDF validated successfully. Page ${data.page} of ${data.totalPages} was rendered and processed. Your result is ready below.` : 'Done. Your image was scanned and processed successfully. Your result is ready below.', 'success');
  } catch (error) {
    message('tryStatus', error.message, 'error');
  } finally {
    button.disabled = false; button.textContent = oldLabel;
  }
});
