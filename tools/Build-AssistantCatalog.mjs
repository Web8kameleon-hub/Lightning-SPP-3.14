// Build public UI translations through a configured Translation Node, never customer content.
// Example: node tools/Build-AssistantCatalog.mjs http://127.0.0.1:18036 artifacts/assistant-catalog.json
import {readFile, writeFile, rename, mkdir} from 'node:fs/promises';
import {dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {validateCatalog} from '../Web/wwwroot/assistant-i18n.mjs';

export async function buildCatalog(endpoint, output, sourceFile = new URL('../Web/wwwroot/assistant-locales.json', import.meta.url)) {
  const base = new URL(endpoint);
  if (!['https:', 'http:'].includes(base.protocol) || base.username || base.password || base.search || base.hash ||
      (base.protocol === 'http:' && !['127.0.0.1','localhost','[::1]'].includes(base.hostname)))
    throw new Error('Use TLS or a loopback SSH tunnel; credentials do not belong in the endpoint URL');
  try { await readFile(output); throw new Error('Output already exists; choose a new artifact path'); }
  catch (error) { if (error.code !== 'ENOENT') throw error; }
  const catalog = validateCatalog(JSON.parse(await readFile(sourceFile, 'utf8')));
  const source = catalog.locales[catalog.defaultLocale];
  const fingerprint = createHash('sha256').update(JSON.stringify({endpoint:base.href,source})).digest('hex');
  const checkpoint = output + '.checkpoint.json';
  let cache = {fingerprint, values:{}, methods:{}};
  try { const previous = JSON.parse(await readFile(checkpoint, 'utf8')); if (previous.fingerprint === fingerprint) cache = previous; }
  catch (error) { if (error.code !== 'ENOENT') throw error; }
  async function call(path, body) {
    const response = await fetch(new URL(path, base), {redirect:'error', signal:AbortSignal.timeout(120000),
      headers:{...(body ? {'Content-Type':'application/json'} : {}), ...(process.env.SPP_TRANSLATION_TOKEN ? {Authorization:'Bearer '+process.env.SPP_TRANSLATION_TOKEN} : {})},
      ...(body ? {method:'POST',body:JSON.stringify(body)} : {})});
    if (!response.ok) throw new Error('Translation Node HTTP ' + response.status + '; no alternate provider or untranslated success substituted');
    const text = await response.text(); if (Buffer.byteLength(text) > 1024*1024) throw new Error('Translation response too large');
    return JSON.parse(text);
  }
  const capabilities = await call('/api/v1/languages');
  if (!capabilities.languages || !Object.keys(capabilities.languages).length) throw new Error('Missing provider language metadata');
  catalog.responseLanguages = Object.fromEntries(Object.entries(capabilities.languages).map(([tag, meta]) => [tag,{name:meta.name}]));
  const placeholders = text => (text.match(/\{[a-zA-Z]+\}/g) || []).sort().join();
  async function translate(text, target) {
    const key = JSON.stringify([target,text]);
    if (cache.values[key] !== undefined) return cache.values[key];
    const result = await call('/api/v1/translate', {text,source:catalog.defaultLocale,target});
    if (result.source_lang !== catalog.defaultLocale || result.target_lang !== target || typeof result.translated !== 'string' || !result.translated.trim() ||
        placeholders(text) !== placeholders(result.translated)) throw new Error('Translation contract or placeholder validation failed for ' + target);
    // Provider confidence values are not measured accuracy and are deliberately not copied.
    cache.values[key] = result.translated; cache.methods[key] = result.method || 'UNREPORTED';
    await mkdir(dirname(output), {recursive:true});
    await writeFile(checkpoint+'.tmp', JSON.stringify(cache)); await rename(checkpoint+'.tmp', checkpoint);
    return result.translated;
  }
  for (const tag of Object.keys(capabilities.languages)) {
    if (catalog.locales[tag]) continue;
    const entry = {label:new Intl.DisplayNames([tag],{type:'language'}).of(tag),direction:new Intl.Locale(tag).textInfo.direction,messages:{},profiles:{}};
    for (const [key,value] of Object.entries(source.messages)) entry.messages[key] = await translate(value,tag);
    for (const [path,profile] of Object.entries(source.profiles)) {
      entry.profiles[path] = {...profile,role:await translate(profile.role,tag),asks:[],links:[]};
      for (const ask of profile.asks) entry.profiles[path].asks.push(await translate(ask,tag));
      for (const [url,label] of profile.links) entry.profiles[path].links.push([url,await translate(label,tag)]);
    }
    catalog.locales[tag] = entry;
    console.log('Translated catalog:',tag);
  }
  catalog.translationBuild = {sourceFingerprint:fingerprint,languages:Object.keys(capabilities.languages).length,
    generatedAt:new Date().toISOString(),review:'MACHINE_TRANSLATED_NOT_NATIVE_REVIEWED'};
  validateCatalog(catalog);
  await mkdir(dirname(output),{recursive:true});
  await writeFile(output,JSON.stringify(catalog,null,2)+'\n',{flag:'wx'});
  return catalog;
}

if (process.argv[1] && new URL(import.meta.url).pathname.toLowerCase().endsWith(process.argv[1].replaceAll('\\','/').toLowerCase())) {
  if (process.argv.length !== 4) {console.error('Usage: node tools/Build-AssistantCatalog.mjs <translation-node-url> <new-output.json>');process.exitCode=2;}
  else try { await buildCatalog(process.argv[2],process.argv[3]); }
  catch (error) {console.error(error.message);process.exitCode=1;}
}
