// Local-only browser acceptance test. It uses the real served page, shared UI
// script and backend. The injected module drives test fixtures, never results.
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';
const backend = process.env.SPP_BROWSER_BACKEND || 'http://127.0.0.1:13082';
const output = resolve('artifacts/pdf-product-verification');
const fixture = await readFile(resolve(output, 'two-page.pdf'));
const imageFixture = await readFile(resolve('Web/wwwroot/favicon-32x32.png'));
const edge = process.env.SPP_TEST_BROWSER || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const driver = `
const reports = [];
const check = (value, label) => { if (!value) throw new Error(label); };
const byId = id => document.getElementById(id);
async function upload(bytes, name, type, profile, success) {
  const dt = new DataTransfer(); dt.items.add(new File([bytes], name, {type}));
  byId('fileInput').files = dt.files;
  if (!Array.from(byId('profileSelect').options).some(o => o.value === profile)) {
    const option = document.createElement('option'); option.value = profile; option.textContent = profile;
    byId('profileSelect').append(option);
  }
  byId('profileSelect').value = profile;
  byId('tryForm').requestSubmit();
  const deadline = Date.now() + 60000;
  while (byId('fileInput').disabled && Date.now() < deadline) await new Promise(r => setTimeout(r, 50));
  check(!byId('fileInput').disabled, 'UI request timed out');
  if (success) {
    check(!byId('results').hidden, 'Result is hidden: ' + byId('tryStatus').textContent);
    check(byId('metaInput').textContent.includes('2 pages'), 'Not all pages processed');
    check(byId('metaProfile').textContent === profile, 'Profile mismatch');
    check(byId('afterImage').complete && byId('afterImage').naturalWidth > 0, 'Preview not decoded');
    const download = byId('downloadLink');
    check(download.download === 'two-page-processed.pdf', 'Wrong download filename');
    const data = await (await fetch(download.href)).arrayBuffer();
    check(new TextDecoder().decode(data.slice(0,5)) === '%PDF-', 'Download is not a PDF');
    const hrefBytes = atob(download.href.split(',')[1]);
    check(hrefBytes.length === data.byteLength, 'Download byte count mismatch');
    check((await fetch('/e2e-output/' + profile, {method:'POST',body:data})).ok, 'Download differs from backend output');
    reports.push({scenario:profile,passed:true,pages:2,downloadBytes:data.byteLength,previewWidth:byId('afterImage').naturalWidth});
  } else {
    check(byId('tryStatus').classList.contains('error'), 'Failure was reported as success');
    check(byId('results').hidden, 'Stale success is visible');
    check(!byId('downloadLink').hasAttribute('href'), 'Stale download remains available');
    reports.push({scenario:profile,passed:true,error:byId('tryStatus').textContent});
  }
}
try {
  check(document.title === 'PDF Processor | Lightning SPP', 'Wrong product route');
  check(byId('fileInput').accept === 'application/pdf,.pdf', 'Wrong upload contract');
  check(Array.from(byId('profileSelect').options).map(o => o.value).join(',') === byId('tryForm').dataset.profiles, 'Registry profile contract mismatch');
  const bytes = Uint8Array.from(atob('${fixture.toString('base64')}'), c => c.charCodeAt(0));
  for (const profile of byId('tryForm').dataset.profiles.split(',')) await upload(bytes,'two-page.pdf','application/pdf',profile,true);
  await upload(new TextEncoder().encode('%PDF-1.7\\nbroken fixture'),'broken.pdf','application/pdf','lightning',false);
  await upload(bytes,'two-page.pdf','application/pdf','unsupported-option',false);
  await upload(new Uint8Array(8*1024*1024+1),'oversized.pdf','application/pdf','lightning',false);
  await upload(new Uint8Array([1,2,3]),'image.png','image/png','lightning',false);
  // The shared script must also preserve the existing image /try workflow.
  const frame = document.createElement('iframe'); frame.src = '/try';
  const loaded = new Promise(resolve => frame.onload = resolve); document.body.append(frame); await loaded;
  const imageDoc = frame.contentDocument, imageWindow = frame.contentWindow;
  const imageBytes = Uint8Array.from(atob('${imageFixture.toString('base64')}'), c => c.charCodeAt(0));
  const files = new imageWindow.DataTransfer();
  files.items.add(new imageWindow.File([imageBytes], 'image.png', {type:'image/png'}));
  imageDoc.getElementById('fileInput').files = files.files;
  imageDoc.getElementById('tryForm').requestSubmit();
  const imageDeadline = Date.now() + 30000;
  while (imageDoc.getElementById('fileInput').disabled && Date.now() < imageDeadline) await new Promise(r => setTimeout(r,50));
  check(!imageDoc.getElementById('results').hidden, 'Existing image workflow regressed');
  const imageDownload = await (await fetch(imageDoc.getElementById('downloadLink').href)).arrayBuffer();
  check(new Uint8Array(imageDownload)[0] === 137, 'Existing image download is not PNG');
  check((await fetch('/e2e-output/image-lightning', {method:'POST',body:imageDownload})).ok, 'Image download differs from backend');
  reports.push({scenario:'existing-image-try-regression',passed:true,downloadBytes:imageDownload.byteLength});
  await fetch('/e2e-report', {method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({passed:true,reports})});
} catch (error) {
  await fetch('/e2e-report', {method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({passed:false,error:error.message,reports})});
}
`;
let resolveReport;
const generatedOutputs = new Map();
const result = new Promise(resolve => { resolveReport = resolve; });
const server = createServer(async (req, res) => {
  try {
    if (req.url === '/e2e-driver.js') { res.setHeader('Content-Type','text/javascript'); res.end(driver); return; }
    if (req.url === '/e2e-report' || req.url.startsWith('/e2e-output/')) {
      const chunks = []; for await (const chunk of req) chunks.push(chunk);
      const bytes = Buffer.concat(chunks);
      if (req.url === '/e2e-report') {
        const report = JSON.parse(bytes); await writeFile(resolve(output,'browser-e2e.json'),JSON.stringify(report,null,2));
        resolveReport(report);
      } else {
        const profile = req.url.slice('/e2e-output/'.length);
        if (!['lightning','tidewave','resonance','nanodecibel','image-lightning'].includes(profile)) throw new Error('Unknown test output');
        if (!generatedOutputs.get(profile)?.equals(bytes)) throw new Error('Download differs from actual backend output');
        await writeFile(resolve(output,'browser-' + profile + (profile.startsWith('image-') ? '.png' : '.pdf')),bytes);
        await writeFile(resolve(output,'browser-' + profile + '.sha256'),createHash('sha256').update(bytes).digest('hex'));
      }
      res.end('ok'); return;
    }
    const init = {method:req.method,headers:req.headers};
    delete init.headers.host;
    if (req.method === 'POST') { init.body = req; init.duplex = 'half'; }
    const response = await fetch(backend + req.url,init);
    res.statusCode = response.status;
    for (const [key,value] of response.headers) if (!['content-length','transfer-encoding','connection'].includes(key)) res.setHeader(key,value);
    let bytes = Buffer.from(await response.arrayBuffer());
    if (req.url === '/api/try/process' && response.ok) {
      const data = JSON.parse(bytes);
      if (data.outputPdfBase64) generatedOutputs.set(data.profile,Buffer.from(data.outputPdfBase64,'base64'));
      else if (data.afterImageBase64) generatedOutputs.set('image-' + data.profile,Buffer.from(data.afterImageBase64,'base64'));
    }
    if (req.url === '/pdf') bytes = Buffer.from(bytes.toString().replace('</body>','<script type="module" src="/e2e-driver.js"></script></body>'));
    res.end(bytes);
  } catch (error) { res.statusCode = 500; res.end(error.message); }
});
await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
const browserProfile = resolve(output,'browser-profile-' + Date.now());
await mkdir(browserProfile,{recursive:true});
const child = spawn(edge,['--headless=new','--disable-gpu','--no-first-run',
  '--user-data-dir=' + browserProfile,
  'http://127.0.0.1:' + server.address().port + '/pdf'], {windowsHide:true,stdio:['ignore','ignore','pipe']});
child.stderr.on('data', () => {});
const timeout = setTimeout(() => resolveReport({passed:false,error:'Browser acceptance timed out'}),90000);
child.on('error',error => resolveReport({passed:false,error:error.message}));
const report = await result;
clearTimeout(timeout);
child.kill(); server.closeAllConnections(); server.close();
console.log(JSON.stringify(report,null,2));
if (!report.passed) process.exitCode = 1;
