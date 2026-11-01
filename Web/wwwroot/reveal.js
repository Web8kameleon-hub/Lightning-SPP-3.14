/* reveal.js — drop-in scroll reveal for Theme Pack 24.
   Markup: <div data-reveal="theme" data-reveal-index="0">…</div>
     data-reveal        keyframe name, or "theme" to use the theme's --tp-reveal
     data-reveal-index  stagger position (index x --tp-stagger)
     data-reveal-dur    override --tp-dur-slow
   Put data-reveal-root on a scroll container to observe inside it instead of the viewport.
   Reveals fire once. Nav, tab bars and above-the-fold app chrome must never carry data-reveal. */
export function initReveals(scope = document) {
  if (!window.IntersectionObserver) return () => {};
  const root = scope.documentElement || scope;
  const groups = new Map();
  scope.querySelectorAll('[data-reveal]').forEach((el) => {
    el.style.animation = 'none';
    el.style.opacity = '0';
    const scroller = el.closest('[data-reveal-root]') || null;
    if (!groups.has(scroller)) groups.set(scroller, []);
    groups.get(scroller).push(el);
  });
  const observers = [];
  groups.forEach((els, scroller) => {
    const o = new IntersectionObserver((entries) => {
      entries.forEach((en) => {
        if (!en.isIntersecting) return;
        const el = en.target;
        const cs = getComputedStyle(root);
        const name = el.dataset.reveal === 'theme'
          ? (cs.getPropertyValue('--tp-reveal').trim() || 'tp-up')
          : el.dataset.reveal;
        const dur = el.dataset.revealDur || cs.getPropertyValue('--tp-dur-slow').trim() || '500ms';
        const ease = cs.getPropertyValue('--tp-ease').trim() || 'ease';
        const stagger = parseFloat(cs.getPropertyValue('--tp-stagger')) || 60;
        const index = Number(el.dataset.revealIndex || 0);
        el.style.opacity = '';
        el.style.animation = name + ' ' + dur + ' ' + ease + ' ' + Math.round(index * stagger) + 'ms both';
        o.unobserve(el);
      });
    }, { root: scroller, threshold: 0.2 });
    els.forEach((el) => o.observe(el));
    observers.push(o);
  });
  return () => observers.forEach((o) => o.disconnect());
}
