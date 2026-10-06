import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const origin = 'https://scanprocessprint.com';
const pages = { '/': 'landing', '/features': 'features', '/technology': 'technology', '/try': 'try', '/pricing': 'pricing', '/benchmarks': 'benchmarks', '/swagger': 'swagger' };
const sitemap = await readFile('Web/wwwroot/sitemap.xml', 'utf8');
const titles = new Set();
for (const [path, name] of Object.entries(pages)) {
  const html = await readFile(`Web/wwwroot/${name}.html`, 'utf8');
  const titlesFound = [...html.matchAll(/<title>(.*?)<\/title>/gs)];
  assert.equal(titlesFound.length, 1, `${path}: one title required`);
  assert(!titles.has(titlesFound[0][1]), `${path}: duplicate title`);
  titles.add(titlesFound[0][1]);
  assert.equal([...html.matchAll(/rel="canonical"/g)].length, 1, `${path}: one canonical required`);
  assert(html.includes(`rel="canonical" href="${origin}${path}"`), `${path}: canonical mismatch`);
  assert(/<meta name="description" content="[^"]+"/.test(html), `${path}: description missing`);
  assert.equal([...html.matchAll(/<h1[ >]/g)].length, 1, `${path}: one main heading required`);
  assert(!/<meta[^>]+name="robots"[^>]+noindex/i.test(html), `${path}: unexpectedly noindex`);
  assert(sitemap.includes(`<loc>${origin}${path}</loc>`), `${path}: missing from sitemap`);
  for (const script of html.matchAll(/<script[^>]+type="application\/ld\+json"[^>]*>(.*?)<\/script>/gs)) {
    JSON.parse(script[1]);
  }
}
const home = await readFile('Web/wwwroot/landing.html', 'utf8');
assert(home.includes('<title>Lightning SPP |'), 'Homepage must name the product first');
assert(home.includes('https://www.clisonix.com/'), 'Related Clisonix link missing');
assert.equal(new Set([...sitemap.matchAll(/<loc>(.*?)<\/loc>/g)].map(match => match[1])).size, Object.keys(pages).length);
console.log('Seven public pages passed title, description, canonical, heading, JSON-LD and sitemap checks.');
