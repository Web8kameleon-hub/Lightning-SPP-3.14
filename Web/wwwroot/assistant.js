'use strict';
// Lightning SPP chameleon assistant: one floating entry point on every page.
// It adapts to the current page (context profile, accent, suggestions) and, only when the
// user switches it on, reads the visible page text (Clisonix Matia pattern: visible <main>
// text only, never inputs, passwords or hidden nodes). Answers come exclusively from the
// server-side model route behind /api/workflow/conversation; nothing is generated locally.
(() => {
  if (window.__lsppAssistant) return; window.__lsppAssistant = true;
  const sq = (navigator.language || '').toLowerCase().startsWith('sq');
  const T = sq ? {
    open: 'Hap asistentin', close: 'Mbyll', send: 'Dërgo', placeholder: 'Pyet për këtë faqe ose për punën tënde…',
    read: 'Lexo ekranin', readHint: n => `Do të dërgohen ${n} karaktere teksti të dukshëm nga kjo faqe. Fushat e formularëve dhe fjalëkalimet nuk lexohen kurrë.`,
    nothing: 'Nuk ka tekst të dukshëm për t’u lexuar në këtë faqe.', preview: 'Shiko çfarë dërgohet',
    live: model => `Model lokal në server: ${model}`, thinking: 'Duke menduar…',
    signIn: 'Hyr në llogari për asistentin privat. Licenca aktive e shërbimit nuk të identifikon automatikisht.', signInLink: 'Hyr në llogari',
    denied: 'Je i identifikuar, por kjo llogari nuk ka leje për workspace. Kontakto administratorin.',
    disabled: 'Asistenti me AI nuk është aktivizuar ende në këtë version. Lidhjet më poshtë funksionojnë.',
    noModel: 'Nuk ka model të konfiguruar në server ende.', error: 'Kërkesa nuk përfundoi. Asgjë nuk u ekzekutua; provo përsëri.',
    review: 'Përgjigje modeli — kontrolloje para se të veprosh.', workspace: 'Hap në Workspace', shortcuts: 'Shkurtore',
  } : {
    open: 'Open assistant', close: 'Close', send: 'Send', placeholder: 'Ask about this page or your work…',
    read: 'Read my screen', readHint: n => `${n} characters of visible text from this page will be sent. Form fields and passwords are never read.`,
    nothing: 'There is no visible text to read on this page.', preview: 'Preview what is sent',
    live: model => `Server-local model: ${model}`, thinking: 'Thinking…',
    signIn: 'Sign in to use your private assistant. An active service license does not sign you in.', signInLink: 'Sign in',
    denied: 'You are signed in, but this account does not have workspace access. Contact your workspace administrator.',
    disabled: 'The AI assistant is not enabled on this release yet. The links below work.',
    noModel: 'No model is configured on the server yet.', error: 'The request did not complete. Nothing was executed; try again.',
    review: 'Model answer — review before acting.', workspace: 'Open in Workspace', shortcuts: 'Shortcuts',
  };
  // Context profiles: the chameleon changes accent, role and suggestions per page.
  const P = (role, roleSq, accent, asks, asksSq, links) => ({ role: sq ? roleSq : role, accent, asks: sq ? asksSq : asks, links });
  const profiles = {
    '/': P('Product guide', 'Udhërrëfyes produkti', '#3bd5ff', ['What can Lightning SPP do for my documents?', 'Summarize this page for me'], ['Çfarë mund të bëjë Lightning SPP për dokumentet e mia?', 'Përmblidhe këtë faqe'], [['/try', 'Try it'], ['/pricing', 'Pricing']]),
    '/features': P('Feature explainer', 'Shpjegues aftësish', '#3bd5ff', ['Which feature fits my use case?'], ['Cila aftësi i përshtatet rastit tim?'], [['/try', 'Try it']]),
    '/technology': P('Technical advisor', 'Këshilltar teknik', '#8b5cf6', ['Explain this architecture simply', 'How do I integrate the SDK?'], ['Shpjegoma këtë arkitekturë thjesht', 'Si e integroj SDK-në?'], [['/swagger', 'API'], ['/benchmarks', 'Benchmarks']]),
    '/try': P('Processing assistant', 'Asistent përpunimi', '#3ddc84', ['Explain the result on my screen', 'Why was my file rejected?'], ['Shpjegoma rezultatin në ekran', 'Pse u refuzua skedari im?'], [['/pdf', 'PDF'], ['/pricing', 'Pricing']]),
    '/pdf': P('PDF assistant', 'Asistent PDF', '#3ddc84', ['Explain the PDF result on my screen'], ['Shpjegoma rezultatin PDF në ekran'], [['/try', 'Images']]),
    '/pricing': P('Plan advisor', 'Këshilltar planesh', '#ff9b38', ['Which plan fits my volume?', 'What happens after I pay?'], ['Cili plan i përshtatet volumit tim?', 'Çfarë ndodh pasi paguaj?'], [['/account', 'Account']]),
    '/benchmarks': P('Benchmark reader', 'Lexues matjesh', '#8b5cf6', ['What do these measurements prove — and not prove?'], ['Çfarë provojnë këto matje — dhe çfarë jo?'], [['/technology', 'Technology']]),
    '/account': P('Account helper', 'Ndihmës llogarie', '#ff9b38', ['How do I activate my license?', 'I did not get the verification email'], ['Si e aktivizoj licencën?', 'Nuk më erdhi emaili i verifikimit'], [['/dashboard', 'License'], ['/workspace', 'Workspace']]),
    '/dashboard': P('License helper', 'Ndihmës licence', '#ff9b38', ['Explain my license status'], ['Shpjegoma statusin e licencës'], [['/account', 'Account']]),
    '/workspace': P('Workflow copilot', 'Kopilot workflow', '#8b5cf6', ['Propose a plan for the selected files', 'What does this job result mean?'], ['Propozo një plan për skedarët e zgjedhur', 'Çfarë do të thotë rezultati i këtij job-i?'], [['/account', 'Account']]),
    '/swagger': P('API helper', 'Ndihmës API', '#8b5cf6', ['Show me how to call /api/try/process'], ['Më trego si thirret /api/try/process'], [['/technology', 'Technology']]),
  };
  const path = (location.pathname.replace(/\/+$/, '').replace(/\.html$/, '') || '/');
  const profile = profiles[path] || profiles['/'];

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
  const form = h('form', { class: 'lspp-form' }, input, sendBtn);
  const panel = h('section', { class: 'lspp-panel', role: 'dialog', 'aria-modal': 'false', 'aria-labelledby': 'lsppTitle', hidden: '' },
    h('header', {}, h('div', {}, h('span', { class: 'lspp-dot', 'aria-hidden': 'true' }), h('strong', { id: 'lsppTitle' }, profile.role)),
      h('button', { type: 'button', class: 'lspp-x', 'aria-label': T.close, onclick: () => toggle(false) }, '×')),
    state, log, chips,
    h('div', { class: 'lspp-read' }, readToggle, h('label', { for: 'lsppRead' }, T.read)), readHint, previewBox, form, links);
  const launcher = h('button', { type: 'button', class: 'lspp-launch', 'aria-label': T.open, 'aria-expanded': 'false', onclick: () => toggle() },
    h('svg', { viewBox: '0 0 32 32', 'aria-hidden': 'true' }));
  launcher.firstChild.innerHTML = '<path d="M6 17c0-6 4.5-10 10-10s10 4 10 9-4 9-9.5 9c-1.6 0-3-.3-4.2-.8L7 26l1.4-4.2C6.9 20.6 6 18.9 6 17Z" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/><circle cx="20" cy="15" r="1.8" fill="currentColor"/><path d="M11 18.5c1.4 1.4 3.6 1.4 5 0" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>';
  const root = h('div', { class: 'lspp-assist' }, panel, launcher);
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
  let csrf = null, route = null, mode = 'unknown', loaded = false;
  const say = (who, text, note) => { const m = h('div', { class: 'lspp-msg ' + who }, h('p', {}, text)); if (note) m.append(h('small', {}, note)); log.append(m); log.scrollTop = log.scrollHeight; return m; };
  function renderChips() { chips.replaceChildren(); for (const q of profile.asks) chips.append(h('button', { type: 'button', onclick: () => { input.value = q; if (/screen|ekran|faqe|page/i.test(q)) { readToggle.checked = true; updateRead(); } ask(); } }, q)); }
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
      route = routes[0].id; setMode('live', T.live(routes[0].model)); renderChips();
      if (path !== '/workspace') links.append(h('a', { href: '/workspace' }, T.workspace));
    } catch { setMode('error', T.error); }
  }
  function updateRead() {
    const on = readToggle.checked; const text = on ? visibleText() : '';
    readHint.hidden = previewBox.hidden = !on;
    readHint.textContent = text ? T.readHint(text.length) : T.nothing; previewBox.querySelector('pre').textContent = text;
    previewBox.hidden = !text;
  }
  readToggle.addEventListener('change', updateRead);
  async function ask() {
    const goal = input.value.trim();
    if (goal && latestPdf && path === '/pdf' && goal === profile.asks[0]) {
      say('me', goal); input.value = ''; grow();
      const d = latestPdf, size = Math.round(1000 * (d.afterBytes / d.originalBytes - 1)) / 10;
      const text = sq
        ? `${d.pages} faqe u përpunuan në ${(d.totalTimeMs / 1000).toFixed(2)} sekonda. Madhësia: ${(d.originalBytes / 1024).toFixed(1)} → ${(d.afterBytes / 1024).toFixed(1)} KiB (${size >= 0 ? '+' : ''}${size}%). PDF-ja është rasterizuar; teksti i përzgjedhshëm, vektorët dhe formularët nuk ruhen. Këto matje nuk provojnë përmirësim cilësie ose optimizim madhësie.`
        : `${d.pages} page(s) processed in ${(d.totalTimeMs / 1000).toFixed(2)} seconds. Size: ${(d.originalBytes / 1024).toFixed(1)} → ${(d.afterBytes / 1024).toFixed(1)} KiB (${size >= 0 ? '+' : ''}${size}%). The PDF was rasterized; selectable text, vectors and forms are not preserved. These measurements do not establish better quality or size optimization.`;
      const message = say('ai', text, sq ? 'Nga përgjigjja e fundit e përpunimit; pa inferencë modeli.' : 'From the latest processing response; no model inference.');
      message.append(h('a', { href: '/pdf#results' }, sq ? 'Burimi: rezultati i këtij përpunimi' : 'Source: this processing result'));
      return;
    }
    if (!goal || mode !== 'live') return;
    say('me', goal); input.value = ''; grow(); sendBtn.disabled = true; root.dataset.busy = 'true';
    const pending = say('ai', T.thinking);
    const screen = readToggle.checked ? { page: path, title: document.title, text: visibleText() } : null;
    try {
      const r = await fetch('/api/workflow/conversation', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-Token': csrf },
        body: JSON.stringify({ route, goal: `[${profile.role} · ${path}] ${goal}`, allowModel: true, ...(screen && screen.text ? { screen } : {}) }), signal: AbortSignal.timeout(200000) });
      const data = await r.json().catch(() => ({}));
      if (!r.ok) throw new Error(data.error || T.error);
      pending.replaceChildren(h('p', {}, data.text || data.Text || ''), h('small', {}, T.review + (data.identity?.model ? ' · ' + data.identity.model : '')));
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
  window.addEventListener('lightning:account-changed', () => { loaded = false; if (!panel.hidden) load(); });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && !panel.hidden) toggle(false);
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k' && !e.altKey) { e.preventDefault(); toggle(true); }
  });
})();
