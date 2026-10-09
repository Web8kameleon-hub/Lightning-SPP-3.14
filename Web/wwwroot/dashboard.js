'use strict';
const byId = id => document.getElementById(id);
const plans = {free:'Free', paygo:'Pay as you go', payasyougo:'Pay as you go', unlimited:'Unlimited', enterprise:'Enterprise'};
const isTrue = value => String(value).toLowerCase() === 'true';
function parse(text) {
  const data = Object.create(null);
  for (const line of text.split('\n')) {
    const index = line.indexOf('=');
    if (index > 0) data[line.slice(0,index).trim()] = line.slice(index+1).trim();
  }
  return data;
}
function message(id, text, kind = '') { const el = byId(id); el.textContent = text; el.className = 'message ' + kind; }
async function request(path, options = {}) {
  let response;
  try { response = await fetch(path, {...options, signal: AbortSignal.timeout(10000)}); }
  catch { throw new Error('We could not reach your workspace. Check your connection and try again.'); }
  if (response.status === 401) throw new Error('The administrator access code is missing or incorrect. Check it and try again.');
  if (response.status === 429) throw new Error('There have been several requests in a short time. Please wait a minute and try again.');
  if (response.status === 400) throw new Error('We could not accept these details. Check the license code or form and try again.');
  if (!response.ok) throw new Error('We could not complete this request. Please try again. If it continues, contact your workspace administrator.');
  return parse(await response.text());
}
async function refreshStatus() {
  byId('refresh').disabled = true;
  message('status','Checking your license…');
  try {
    const data = await request('/api/license/status');
    if (!('valid' in data) || !('hasLicense' in data)) throw new Error('Your license information is unavailable. Please try again.');
    const valid = isTrue(data.valid);
    const exists = isTrue(data.hasLicense);
    message('status', valid ? 'Your license is active.' : exists ? 'Your license is no longer active.' : 'No license has been activated yet.', valid ? 'success' : '');
    byId('plan').textContent = plans[String(data.tier).toLowerCase()] || 'Contact your administrator';
    const uses = Number(data.usageThisMonth);
    byId('usage').textContent = Number.isFinite(uses) && uses >= 0 ? uses.toLocaleString('en') : 'Unavailable';
    byId('nextStep').textContent = valid ? 'You can continue in the connected app your organization uses for scanning and printing.' : 'Ask your workspace administrator to activate a license. If you manage this workspace, open “Workspace administration” below.';
  } catch (error) {
    message('status',error.message,'error');
    byId('plan').textContent = 'Unavailable'; byId('usage').textContent = 'Unavailable';
    byId('nextStep').textContent = 'Choose “Check again” to retry. If the problem continues, contact your workspace administrator.';
  } finally { byId('refresh').disabled = false; }
}
async function submitLicense(event, kind) {
  event.preventDefault();
  const output = kind === 'activate' ? 'activation' : 'newKey';
  const needsAdmin = kind !== 'activate';
  const token = needsAdmin ? byId('adminToken').value.trim() : '';
  if (needsAdmin && !token) { message(output,'Enter your administrator access code above to continue.','error'); byId('adminToken').focus(); return; }
  const button = event.currentTarget.querySelector('button');
  const oldLabel = button.textContent;
  button.disabled = true; button.textContent = kind === 'activate' ? 'Activating…' : 'Creating…';
  message(output,'Please wait…');
  if (kind === 'issue') { byId('createdLicense').hidden = true; byId('generatedKey').value = ''; }
  try {
    const payload = kind === 'activate' ? {key:byId('licenseKey').value.trim()} : {email:byId('email').value.trim(),company:byId('company').value.trim(),tier:byId('tier').value};
    const headers = {'Content-Type':'application/x-www-form-urlencoded'};
    if (needsAdmin) headers.Authorization = 'Bearer '+token;
    const data = await request('/api/license/' + kind, {method:'POST',headers,body:new URLSearchParams(payload)});
    if (!isTrue(data.ok)) throw new Error('The license could not be saved. Please check your details and try again.');
    if (kind === 'issue') {
      if (!data.key) throw new Error('No license code was returned. Please contact your workspace administrator.');
      byId('generatedKey').value = data.key; byId('licenseKey').value = data.key; byId('createdLicense').hidden = false;
      message(output,'Your license code is ready. Use “Activate license” to apply it to this workspace.','success');
    } else { message(output,'License activated successfully. Your workspace status has been updated.','success'); await refreshStatus(); }
  } catch(error) { message(output,error.message,'error'); }
  finally { button.disabled = false; button.textContent = oldLabel; }
}
byId('refresh').addEventListener('click',refreshStatus);
byId('activateForm').addEventListener('submit',event => submitLicense(event,'activate'));
byId('issueForm').addEventListener('submit',event => submitLicense(event,'issue'));
byId('loadDiagnostics').addEventListener('click',async () => {
  const button = byId('loadDiagnostics'); button.disabled = true; message('diagnostics','Loading service details…');
  try { const data = await request('/api/metrics'); message('diagnostics','Requests: '+(data.pipelineRequests || '0')+'\nCompleted: '+(data.pipelineSuccess || '0')+'\nFailed: '+(data.pipelineFailure || '0')); }
  catch(error) { message('diagnostics',error.message,'error'); }
  finally { button.disabled = false; }
});
if (location.hash === '#administration') byId('administration').open = true;
if (new URLSearchParams(location.search).get('purchase') === 'success') {
  byId('purchaseStatus').textContent = 'Stripe returned you here after checkout. This redirect does not prove a license was provisioned or activated. Sign in to check your customer account. If you already paid, do not pay again.';
}
fetch('/api/account', {signal:AbortSignal.timeout(10000)}).then(async response => {
  if (!response.ok) return;
  const account = await response.json();
  byId('purchaseStatus').textContent = account.entitlements.length ?
    'Your account entitlements: ' + account.entitlements.map(e => e.tier + ' — ' + e.state).join('; ') :
    'No paid entitlement is currently linked to your account. If you already paid, contact support to reconcile that purchase; do not pay again.';
}).catch(() => { /* The visible message continues to require an account check. */ });
refreshStatus();
