'use strict';
// Pattern reviewed in Clisonix Matia's visible-page reader; restricted to this page,
// opt-in and no hidden/input/password content. No background capture or inference.
window.lightningVisibleContext = () => {
  const root = document.querySelector('main');
  if (!root) return '';
  return Array.from(root.querySelectorAll('h1,h2,h3,p,li'))
    .filter(node => !node.closest('[hidden]') && node.getClientRects().length && getComputedStyle(node).visibility !== 'hidden')
    .map(node => node.innerText).filter(Boolean).join('\n').slice(0, 12000);
};
