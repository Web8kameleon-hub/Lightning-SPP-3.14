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
  if (!file) { message('tryStatus', 'Choose an image file first.', 'error'); return; }
  if (file.size > 8 * 1024 * 1024) { message('tryStatus', 'File is larger than 8 MB. Choose a smaller image.', 'error'); return; }

  const profile = byId('profileSelect').value;
  const button = event.currentTarget.querySelector('button');
  const oldLabel = button.textContent;
  button.disabled = true; button.textContent = 'Processing…';
  message('tryStatus', 'Uploading and running the real pipeline on your image…');
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

    byId('beforeImage').src = URL.createObjectURL(file);
    const afterSrc = 'data:image/png;base64,' + data.afterImageBase64;
    byId('afterImage').src = afterSrc;
    byId('downloadLink').href = afterSrc;

    byId('metaProfile').textContent = data.profile;
    byId('metaScanTime').textContent = data.scanTimeMs + ' ms';
    byId('metaProcessTime').textContent = data.processTimeMs + ' ms';
    byId('metaTotalTime').textContent = data.totalTimeMs + ' ms';
    byId('metaOriginalSize').textContent = formatBytes(data.originalBytes);
    byId('metaCompressedSize').textContent = formatBytes(data.compressedBytes);
    byId('metaCompression').textContent = data.compressionPercent + '%';

    byId('results').hidden = false;
    message('tryStatus', 'Done. Your result is below.', 'success');
  } catch (error) {
    message('tryStatus', error.message, 'error');
  } finally {
    button.disabled = false; button.textContent = oldLabel;
  }
});
