'use strict';
// Lightning SPP chameleon assistant: one floating entry point on every page.
// It adapts to the current page (context profile, accent, suggestions) and, only when the
// user switches it on, reads the visible page text (Clisonix Matia pattern: visible <main>
// text only, never inputs, passwords or hidden nodes). Answers come exclusively from the
// server-side model route behind /api/workflow/conversation; nothing is generated locally.
(async () => {
  if (window.__lsppAssistant) return; window.__lsppAssistant = true;
  const {negotiateLocale, formatMessage, validateCatalog} = await import('/assistant-i18n.mjs');
  const response = await fetch('/assistant-locales.json', {signal: AbortSignal.timeout(10000)});
  if (!response.ok) throw new Error('Assistant language catalog unavailable');
  const catalog = validateCatalog(await response.json());
  let savedLocale;
  try { savedLocale = localStorage.getItem('lightning-assistant-locale'); } catch { /* Storage may be disabled. */ }
  let locale = negotiateLocale([savedLocale, ...(navigator.languages || [navigator.language])].filter(Boolean), Object.keys(catalog.locales), catalog.defaultLocale);
  let entry = catalog.locales[locale], T = entry.messages;
  const text = (key, values) => formatMessage(T[key], values);
  const path = (location.pathname.replace(/\/+$/, '').replace(/\.html$/, '') || '/');
  let profile = entry.profiles[path] || entry.profiles[catalog.defaultProfile];

  // Visible-page reader: same contract as screen-context.js, excluding the assistant itself.
  const visibleText = () => {
    const root = document.querySelector('main') || document.body;
    return Array.from(root.querySelectorAll('h1,h2,h3,h4,p,li,dt,dd,td,th,figcaption,pre,output,.message'))
      .filter(n => !n.closest('[hidden],.lspp-assist,[aria-hidden="true"]') && n.getClientRects().length && getComputedStyle(n).visibility !== 'hidden')
      .map(n => n.innerText.trim()).filter(Boolean)
      .filter((t, i, all) => all.indexOf(t) === i).join('\n').slice(0, 12000);
  };

  const h = (tag, attrs = {}, ...kids) => { const e = document.createElement(tag); for (const [k, v] of Object.entries(attrs)) { if (k === 'class') e.className = v; else if (k.startsWith('on')) e.addEventListener(k.slice(2), v); else e.setAttribute(k, v); } e.append(...kids); return e; };
  const css = document.createElement('link'); css.rel = 'stylesheet'; css.href = '/assistant.css'; document.head.append(css);

  const log = h('div', { class: 'lspp-log', role: 'log', 'aria-live': 'polite' });
  const state = h('p', { class: 'lspp-state' });
  const input = h('textarea', { rows: '1', placeholder: T.placeholder, 'aria-label': T.placeholder, maxlength: '4000' });
  const readToggle = h('input', { type: 'checkbox', id: 'lsppRead' });
  const readHint = h('p', { class: 'lspp-hint', hidden: '' });
  const previewBox = h('details', { class: 'lspp-preview', hidden: '' }, h('summary', {}, T.preview), h('pre'));
  const sendBtn = h('button', { type: 'submit', class: 'lspp-send', 'aria-label': T.send }, '↑');
  const chips = h('div', { class: 'lspp-chips' });
  const links = h('nav', { class: 'lspp-links', 'aria-label': T.shortcuts });
  for (const [href, label] of profile.links) links.append(h('a', { href }, label));
  const languageSelect = h('select', {class: 'lspp-language'});
  for (const [tag, translation] of Object.entries(catalog.locales)) languageSelect.append(h('option', {value: tag}, translation.label));
  languageSelect.value = locale; languageSelect.setAttribute('aria-label', T.language);
  const responseSelect = h('select', {class: 'lspp-language', 'aria-label': T.responseLanguage});
  responseSelect.append(h('option', {value: ''}, T.automaticLanguage));
  for (const [tag, metadata] of Object.entries(catalog.responseLanguages)) {
    let label = metadata.name;
    try { label = new Intl.DisplayNames([tag], {type: 'language'}).of(tag); } catch { /* Use provider-supplied name. */ }
    responseSelect.append(h('option', {value: tag}, label));
  }
  try { const saved = localStorage.getItem('lightning-assistant-response-language'); if (saved in catalog.responseLanguages) responseSelect.value = saved; } catch { }
  responseSelect.addEventListener('change', () => { try { localStorage.setItem('lightning-assistant-response-language', responseSelect.value); } catch { } });
  const responseLabel = h('label', {}, T.responseLanguage, responseSelect);
  const form = h('form' , { class: 'lspp-form' }, input, sendBtn);
  const panel = h('section', { class: 'lspp-panel', role: 'dialog', 'aria-modal': 'false', 'aria-labelledby': 'lsppTitle', hidden: '' },
    h('header', {}, h('div', {}, h('span', { class: 'lspp-dot', 'aria-hidden': 'true' }), h('strong', { id: 'lsppTitle' }, profile.role)),
      h('button', { type: 'button', class: 'lspp-x', 'aria-label': T.close, onclick: () => toggle(false) }, '×')),
    languageSelect, responseLabel, state, log, chips,
    h('div', { class: 'lspp-read' }, readToggle, h('label', { for: 'lsppRead' }, T.read)), readHint, previewBox, form, links);
  const launcher = h('button', { type: 'button', class: 'lspp-launch', 'aria-label': T.open, 'aria-expanded': 'false', onclick: () => toggle() },
    h('svg', { viewBox: '0 0 32 32', 'aria-hidden': 'true' }));
  launcher.firstChild.innerHTML = '<path d="M6 17c0-6 4.5-10 10-10s10 4 10 9-4 9-9.5 9c-1.6 0-3-.3-4.2-.8L7 26l1.4-4.2C6.9 20.6 6 18.9 6 17Z" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/><circle cx="20" cy="15" r="1.8" fill="currentColor"/><path d="M11 18.5c1.4 1.4 3.6 1.4 5 0" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>';
  const root = h('div', { class: 'lspp-assist' }, panel, launcher);
  panel.lang = locale; panel.dir = entry.direction;
  root.style.setProperty('--lspp-accent', profile.accent);
  document.body.append(root);

  let latestPdf = null;
  window.addEventListener('lightning:pdf-result', event => {
    const d = event.detail;
    if (d === null) { latestPdf = null; return; }
    if (path !== '/pdf' || !d || !Number.isInteger(d.pages) || d.pages < 1 || d.pages > 32 ||
        ![d.originalBytes, d.afterBytes, d.totalTimeMs].every(n => Number.isFinite(n) && n >= 0) || d.originalBytes === 0) return;
    latestPdf = { ...d };
  });
  let csrf = null, route = null, automaticQualityRouting = false, mode = 'unknown', loaded = false;
  const say = (who, text, note) => { const m = h('div', { class: 'lspp-msg ' + who }, h('p', {}, text)); if (note) m.append(h('small', {}, note)); log.append(m); log.scrollTop = log.scrollHeight; return m; };
  function renderChips() { chips.replaceChildren(); for (const [index, q] of profile.asks.entries()) chips.append(h('button', { type: 'button', onclick: () => { input.value = q; if (profile.readScreenOnAsk?.includes(index)) { readToggle.checked = true; updateRead(); } ask(); } }, q)); }
  function setMode(next, message, link) {
    mode = next; state.replaceChildren(message); if (link) state.append(' ', h('a', { href: link[0] }, link[1]));
    const live = next === 'live'; input.disabled = sendBtn.disabled = !live; chips.hidden = !live; root.dataset.mode = next;
  }
  async function load() {
    loaded = true; setMode('loading', T.thinking);
    try {
      const r = await fetch('/api/workflow', { credentials: 'same-origin', signal: AbortSignal.timeout(15000) });
      if (r.status === 503) return setMode('disabled', T.disabled);
      if (r.status === 401 || r.status === 403) {
        const failure = await r.json().catch(() => ({}));
        if (failure.code === 'workspace_access_denied') return setMode('denied', T.denied);
        return setMode('signin', T.signIn, ['/account', T.signInLink]);
      }
      if (!r.ok) throw new Error();
      const m = await r.json(); csrf = m.csrfToken;
      const routes = Array.isArray(m.models) ? m.models : [];
      if (!routes.length) return setMode('nomodel', T.noModel);
      automaticQualityRouting = m.automaticQualityRouting === true;
      // Existing single-route installations remain compatible. Multiple routes require measured selection.
      if (!automaticQualityRouting && routes.length !== 1) return setMode('nomodel', T.qualityUnavailable);
      route = automaticQualityRouting ? null : routes[0].id;
      setMode('live', automaticQualityRouting ? T.qualityRouting : text('live', {model: routes[0].model})); renderChips();
      if (path !== '/workspace') links.append(h('a', { href: '/workspace' }, T.workspace));
    } catch { setMode('error', T.error); }
  }
  function updateRead() {
    const on = readToggle.checked; const text = on ? visibleText() : '';
    readHint.hidden = previewBox.hidden = !on;
    readHint.textContent = text ? formatMessage(T.readHint, {count: text.length}) : T.nothing; previewBox.querySelector('pre').textContent = text;
    previewBox.hidden = !text;
  }
  readToggle.addEventListener('change', updateRead);
  async function ask() {
    const goal = input.value.trim();
    if (goal && latestPdf && path === '/pdf' && goal === profile.asks[0]) {
      say('me', goal); input.value = ''; grow();
      const d = latestPdf, size = Math.round(1000 * (d.afterBytes / d.originalBytes - 1)) / 10;
      const summary = text('pdfMeasurement', {pages:d.pages,seconds:(d.totalTimeMs/1000).toFixed(2),before:(d.originalBytes/1024).toFixed(1),after:(d.afterBytes/1024).toFixed(1),change:(size>=0?'+':'')+size});
      const message = say('ai', summary, T.pdfMeasurementNote);
      message.append(h('a', { href: '/pdf#results' }, T.pdfMeasurementSource));
      return;
    }
    if (!goal || mode !== 'live') return;
    say('me', goal); input.value = ''; grow(); sendBtn.disabled = true; root.dataset.busy = 'true';
    const pending = say('ai', T.thinking);
    const screen = readToggle.checked ? { page: path, title: document.title, text: visibleText() } : null;
    try {
      const r = await fetch('/api/workflow/conversation', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-Token': csrf },
        body: JSON.stringify({ ...(automaticQualityRouting ? {automaticQualityRouting:true} : {route}), locale, ...(responseSelect.value ? {responseLanguage: responseSelect.value} : {}), goal: `[${profile.role} · ${path}] ${goal}`, allowModel: true, ...(screen && screen.text ? { screen } : {}) }), signal: AbortSignal.timeout(200000) });
      const data = await r.json().catch(() => ({}));
      if (!r.ok) throw new Error(data.error || T.error);
      const answer = data.text || data.Text;
      if (typeof answer !== 'string' || !answer.trim()) throw new Error(T.error);
      pending.replaceChildren(h('p', {}, answer), h('small', {}, T.review + (data.identity?.model ? ' · ' + data.identity.model : '') + (data.quality ? ' · '+T.measuredLanguage : '')));
    } catch (e) { pending.classList.add('err'); pending.replaceChildren(h('p', {}, e.message || T.error)); }
    finally { sendBtn.disabled = false; delete root.dataset.busy; readToggle.checked = false; updateRead(); input.focus(); }
  }
  form.addEventListener('submit', e => { e.preventDefault(); ask(); });
  const grow = () => { input.style.height = 'auto'; input.style.height = Math.min(input.scrollHeight, 140) + 'px'; };
  input.addEventListener('input', grow);
  input.addEventListener('keydown', e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); ask(); } });
  function toggle(force) {
    const open = force ?? panel.hidden; panel.hidden = !open; launcher.setAttribute('aria-expanded', String(open)); root.dataset.open = String(open);
    if (open) { if (!loaded || mode === 'signin' || mode === 'error') load(); setTimeout(() => (input.disabled ? panel.querySelector('.lspp-x') : input).focus(), 60); } else launcher.focus();
  }
  languageSelect.addEventListener('change', () => {
    locale = languageSelect.value; entry = catalog.locales[locale]; T = entry.messages;
    profile = entry.profiles[path] || entry.profiles[catalog.defaultProfile];
    try { localStorage.setItem('lightning-assistant-locale', locale); } catch { /* Keep in-memory preference. */ }
    panel.lang = locale; panel.dir = entry.direction;
    root.style.setProperty('--lspp-accent', profile.accent);
    launcher.setAttribute('aria-label', T.open); panel.querySelector('.lspp-x').setAttribute('aria-label', T.close);
    panel.querySelector('#lsppTitle').textContent = profile.role;
    input.placeholder = T.placeholder; input.setAttribute('aria-label', T.placeholder);
    responseLabel.firstChild.textContent = T.responseLanguage; responseSelect.setAttribute('aria-label', T.responseLanguage); responseSelect.options[0].textContent = T.automaticLanguage;
    sendBtn.setAttribute('aria-label', T.send); languageSelect.setAttribute('aria-label', T.language);
    panel.querySelector('label[for="lsppRead"]').textContent = T.read;
    previewBox.querySelector('summary').textContent = T.preview; links.setAttribute('aria-label', T.shortcuts);
    links.replaceChildren(); for (const [href, label] of profile.links) links.append(h('a', {href}, label));
    updateRead(); renderChips(); load();
  });
  window.addEventListener('lightning:account-changed' , () => { loaded = false; if (!panel.hidden) load(); });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && !panel.hidden) toggle(false);
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k' && !e.altKey) { e.preventDefault(); toggle(true); }
  });
})().catch(error => {
  console.error('Assistant initialization failed:', error.message);
  const warning = document.createElement('p'); warning.className = 'message error'; warning.setAttribute('role', 'alert');
  warning.textContent = 'Assistant language resources could not load. Reload this page to retry.';
  document.body.append(warning);
});
