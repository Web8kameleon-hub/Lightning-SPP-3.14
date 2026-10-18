'use strict';
(() => {
  const el = id => document.getElementById(id);
  let csrf, manifest, jobId, poll, previewUrl, nextOffset = 0;
  const selected = new Set();
  const status = (text, error = false) => { el('workspaceStatus').textContent = text; el('workspaceStatus').className = 'message ' + (error ? 'error' : ''); };
  const consent = () => ({allowModel: el('allowModel').checked, allowRemote: el('allowRemote').checked, allowServices: el('allowServices').checked, allowPrint: el('allowPrint').checked});
  async function api(route, body) {
    const response = await fetch('/api/workflow' + route, body === undefined ? {signal: AbortSignal.timeout(20000)} : {
      method: 'POST', headers: {'Content-Type': 'application/json', 'X-CSRF-Token': csrf}, body: JSON.stringify({...body, ...consent()}), signal: AbortSignal.timeout(600000)
    });
    const data = await response.json();
    if (!response.ok) throw new Error(data.error || 'Workspace operation did not complete. Check job state before retrying.');
    return data;
  }
  async function busy(button, action) { button.disabled = true; try { await action(); } catch (error) { status(error.message, true); } finally { button.disabled = button.dataset.locked === 'true'; } }
  function plan() { const value = JSON.parse(el('workflowPlan').value); if (!Array.isArray(value.steps)) throw new Error('A plan requires a steps array.'); return value; }
  function draftChanged() { jobId = undefined; el('runPlan').disabled = true; el('runPlan').dataset.locked = 'true'; }
  function renderJob(job) {
    jobId = job.id; el('runPlan').disabled = job.state === 'RUNNING' || job.state === 'SUCCEEDED';
    el('runPlan').dataset.locked = String(el('runPlan').disabled);
    const completed = job.steps.filter(step => step.state === 'SUCCEEDED').length;
    el('jobProgress').textContent = job.state + ' · ' + completed + '/' + job.steps.length + ' steps completed · ' + job.updated;
    el('jobResults').textContent = JSON.stringify(job, null, 2); el('reviewFindings').replaceChildren();
    for (const step of job.steps) {
      const result = step.result;
      if (result?.data?.path?.startsWith('reports/')) {
        const link = document.createElement('a'); link.textContent = 'Download report for ' + step.id;
        link.href = '/api/workflow/report?path=' + encodeURIComponent(result.data.path); el('reviewFindings').append(link);
      }
      if (result?.verification !== 'REVIEW_REQUIRED') continue;
      const card = document.createElement('article'); card.className = 'card';
      const activeAlerts = result.data.findings?.some(finding => finding.state === 'REVIEW_ALERT');
      card.setAttribute('role', activeAlerts ? 'alert' : 'status');
      const heading = document.createElement('h3'); heading.textContent = (activeAlerts ? 'Alert from supplied rule: ' : 'Review required: ') + step.id;
      const findings = document.createElement('pre'); findings.textContent = JSON.stringify(result.data, null, 2);
      const note = document.createElement('textarea'); note.setAttribute('aria-label', 'Review note for ' + step.id); note.maxLength = 4000;
      const button = document.createElement('button'); button.type = 'button'; button.textContent = 'Record my acknowledgement';
      const acknowledged = job.reviews?.find(review => review.stepId === step.id && review.resultHash === step.resultHash);
      if (acknowledged) { const receipt = document.createElement('p'); receipt.textContent = 'Acknowledged: ' + acknowledged.acknowledgedUtc + ' · ' + acknowledged.note; card.append(receipt); }
      button.addEventListener('click', () => busy(button, async () => { await api('/acknowledge', {id: job.id, stepId: step.id, note: note.value}); status('Acknowledgement recorded. This does not establish domain validation.'); renderJob(await api('/job?id=' + job.id)); }));
      card.append(heading, findings, note, button); el('reviewFindings').append(card);
    }
  }
  async function files(offset = 0) {
    const result = await api('/files?offset=' + offset); if (offset === 0) el('workspaceFiles').replaceChildren();
    for (const path of result.data.files) {
      const label = document.createElement('label'); const check = document.createElement('input'); check.type = 'checkbox'; check.checked = selected.has(path);
      check.addEventListener('change', () => check.checked ? selected.add(path) : selected.delete(path)); label.append(check, document.createTextNode(path)); el('workspaceFiles').append(label, document.createElement('br'));
    }
    nextOffset = result.data.nextOffset; el('moreFiles').hidden = nextOffset === null;
  }
  async function loadManifest() {
    manifest = await api(''); csrf = manifest.csrfToken;
    for (const [id, values, name] of [['registeredTool', manifest.tools, 'id'], ['modelRoute', manifest.models, 'id']]) {
      el(id).replaceChildren(); for (const item of values) { const option = document.createElement('option'); option.value = item[name]; option.textContent = item[name]; el(id).append(option); }
    }
    el('savedJobs').replaceChildren(); for (const id of manifest.jobs) { const option = document.createElement('option'); option.value = id; option.textContent = id; el('savedJobs').append(option); }
    el('allowRemote').disabled = !manifest.remoteModelsEnabled;
    el('allowPrint').disabled = !manifest.printEnabled;
    if (!manifest.models.length) {
      const option = document.createElement('option'); option.textContent = 'No model configured for this workspace'; el('modelRoute').append(option);
      for (const id of ['askGuide','proposePlan','discoverModel']) { el(id).disabled = true; el(id).dataset.locked = 'true'; }
    }
    el('workspaceBody').hidden = false; el('workspaceLogin').hidden = true;
    el('registeredTool').dispatchEvent(new Event('change')); await files();
  }
  el('registeredTool').addEventListener('change', () => { el('toolContract').textContent = JSON.stringify(manifest.tools.find(t => t.id === el('registeredTool').value), null, 2); });
  el('workflowPlan').value = JSON.stringify({goal: '', steps: []}, null, 2);
  el('workflowPlan').addEventListener('input', draftChanged);
  el('addTool').addEventListener('click', () => {
    try {
      const current = plan(); current.goal = el('workflowGoal').value || current.goal;
      const contract = manifest.tools.find(t => t.id === el('registeredTool').value); const args = Object.fromEntries(contract.requiredArguments.map(key => [key, '']));
      if ('path' in args && selected.size) args.path = Array.from(selected)[0];
      current.steps.push({id: 'step' + (current.steps.length + 1), tool: contract.id, arguments: args, dependsOn: []});
      el('workflowPlan').value = JSON.stringify(current, null, 2); draftChanged();
    } catch (error) { status(error.message, true); }
  });
  el('importFiles').addEventListener('click', () => busy(el('importFiles'), async () => {
    for (const file of el('workspaceUpload').files) {
      if (file.size > 8 * 1024 * 1024) throw new Error('File exceeds 8 MiB; select a smaller document or batch.');
      const bytes = new Uint8Array(await file.arrayBuffer()); let binary = ''; for (let i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
      const imported = await api('/import', {filename: file.name, base64: btoa(binary)}); selected.add(imported.path);
      if (file.type === 'application/pdf') { if (previewUrl) URL.revokeObjectURL(previewUrl); previewUrl = URL.createObjectURL(file); el('originalPreview').src = previewUrl; el('originalPreview').hidden = false; }
    }
    await files(); status('Original files imported with content hashes. No understanding or inference is implied.');
  }));
  el('refreshFiles').addEventListener('click', () => busy(el('refreshFiles'), () => files()));
  el('moreFiles').addEventListener('click', () => busy(el('moreFiles'), () => files(nextOffset)));
  for (const [id, route] of [['askGuide', '/conversation'], ['proposePlan', '/propose'], ['discoverModel', '/discover']])
    el(id).addEventListener('click', () => busy(el(id), async () => {
      let goal = el('workflowGoal').value;
      if (el('includeScreen').checked) goal += '\nUNTRUSTED_VISIBLE_PAGE_DATA:\n' + window.lightningVisibleContext();
      const result = await api(route, {route: el('modelRoute').value, goal, paths: Array.from(selected), ...(jobId ? {jobId} : {})});
      el('guideReply').textContent = JSON.stringify(result, null, 2);
      if (result.plan) { el('workflowPlan').value = JSON.stringify(result.plan, null, 2); draftChanged(); status('Plan proposed. Review every step before saving and running.'); }
    }));
  el('savePlan').addEventListener('click', () => busy(el('savePlan'), async () => { const job = await api('/plan', {plan: plan()}); renderJob(job); status('Reviewed plan saved. No tool has run yet.'); }));
  el('runPlan').addEventListener('click', () => busy(el('runPlan'), async () => {
    if (!jobId) throw new Error('Save the reviewed plan first.'); const runningId = jobId; el('cancelPlan').disabled = false;
    poll = setInterval(() => api('/job?id=' + runningId).then(renderJob).catch(error => status(error.message, true)), 10000);
    try { const job = await api('/run', {id: runningId}); renderJob(job); status('Job state: ' + job.state); }
    finally { clearInterval(poll); el('cancelPlan').disabled = true; }
  }));
  el('cancelPlan').addEventListener('click', () => busy(el('cancelPlan'), async () => { const result = await api('/cancel', {id: jobId}); status(result.cancellationRequested ? 'Cancellation requested. Check persisted job state.' : 'No active worker accepted cancellation; check job state.'); }));
  el('loadJob').addEventListener('click', () => busy(el('loadJob'), async () => { const job = await api('/job?id=' + encodeURIComponent(el('savedJobs').value)); el('workflowPlan').value = JSON.stringify(job.plan, null, 2); renderJob(job); }));
  window.addEventListener('pagehide', () => { clearInterval(poll); if (previewUrl) URL.revokeObjectURL(previewUrl); });
  loadManifest().then(() => status('Private workspace ready. Select sources and review a plan.')).catch(error => { status(error.message, true); el('workspaceLogin').hidden = false; });
})();
