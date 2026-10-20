// Locale catalog is deployment data; selection never depends on a named language.
export function negotiateLocale(preferences, available, defaultLocale) {
  const names = new Map(available.map(name => [Intl.getCanonicalLocales(name)[0].toLowerCase(), name]));
  const canonicalDefault = Intl.getCanonicalLocales(defaultLocale)[0].toLowerCase();
  if (!names.has(canonicalDefault)) throw new Error('Invalid default locale');
  for (const preference of preferences) {
    let tag;
    try { tag = Intl.getCanonicalLocales(preference)[0]; } catch { continue; }
    while (tag) {
      if (names.has(tag.toLowerCase())) return names.get(tag.toLowerCase());
      tag = tag.includes('-') ? tag.slice(0, tag.lastIndexOf('-')) : '';
    }
  }
  return names.get(canonicalDefault);
}
export function formatMessage(template, values = {}) {
  if (typeof template !== 'string') throw new Error('Missing localized message');
  return template.replace(/\{([a-zA-Z]+)\}/g, (match, key) => String(values[key] ?? match));
}
export function validateCatalog(catalog) {
  if (!catalog?.locales || !catalog.locales[catalog.defaultLocale]) throw new Error('Missing default catalog');
  const base = catalog.locales[catalog.defaultLocale];
  if (!base.profiles?.[catalog.defaultProfile]) throw new Error('Missing default page profile');
  const responseTags = Object.keys(catalog.responseLanguages || {});
  if (!responseTags.length) throw new Error('Missing response-language metadata');
  const canonicalTags = responseTags.map(tag => Intl.getCanonicalLocales(tag)[0]);
  if (new Set(canonicalTags).size !== responseTags.length) throw new Error('Duplicate response-language aliases');
  for (const [locale, entry] of Object.entries(catalog.locales)) {
    Intl.getCanonicalLocales(locale);
    if (!entry.label || !['ltr', 'rtl'].includes(entry.direction)) throw new Error('Invalid locale metadata');
    for (const key of Object.keys(base.messages)) {
      if (typeof entry.messages?.[key] !== 'string' || !entry.messages[key].trim()) throw new Error('Incomplete catalog: ' + locale + '/' + key);
      const placeholders = value => (value.match(/\{[a-zA-Z]+\}/g) || []).sort().join();
      if (placeholders(entry.messages[key]) !== placeholders(base.messages[key])) throw new Error('Translation placeholders differ: ' + locale + '/' + key);
    }
    for (const [path, profile] of Object.entries(base.profiles)) {
      const translated = entry.profiles?.[path];
      if (!translated?.role || translated.asks?.length !== profile.asks.length || translated.asks.some(text => typeof text !== 'string' || !text.trim()) ||
          translated.links?.length !== profile.links.length || translated.links.some((link, i) => link[0] !== profile.links[i][0] || !link[1]))
        throw new Error('Incomplete localized profile: ' + locale + '/' + path);
    }
  }
  return catalog;
}
