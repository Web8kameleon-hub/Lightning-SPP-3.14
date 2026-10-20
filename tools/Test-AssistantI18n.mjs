import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {negotiateLocale, formatMessage, validateCatalog} from '../Web/wwwroot/assistant-i18n.mjs';
import {buildCatalog} from './Build-AssistantCatalog.mjs';
import {createServer} from 'node:http';
import {mkdtemp, writeFile, access, rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
const catalog = JSON.parse(readFileSync(new URL('../Web/wwwroot/assistant-locales.json', import.meta.url), 'utf8'));
test('catalog translations and placeholders are complete for every advertised interface', () => {
  assert.equal(validateCatalog(catalog), catalog);
  for (const tag of Object.keys(catalog.responseLanguages)) {
    const data = {locale: tag};
    assert.equal(JSON.parse(JSON.stringify(data)).locale, tag);
    assert.doesNotThrow(() => Intl.getCanonicalLocales(tag));
  }
});
test('regional negotiation, aliases, stored choice and configured default do not depend on named languages', () => {
  assert.equal(negotiateLocale(['sq-AL', 'de-DE'], ['en','sq','de'], 'en'), 'sq');
  assert.equal(negotiateLocale(['pt-BR'], ['en','pt'], 'en'), 'pt');
  assert.equal(negotiateLocale(['fil-PH'], ['tl'], 'tl'), 'tl');
  assert.equal(negotiateLocale(['invalid!', 'ja-JP'], ['ar','ja'], 'ar'), 'ja');
  assert.equal(negotiateLocale(['fi'], ['fr','it'], 'it'), 'it');
});
test('missing translation or altered placeholder fails instead of silently cloning English', () => {
  const missing = structuredClone(catalog); delete missing.locales.sq.messages.send;
  assert.throws(() => validateCatalog(missing));
  const altered = structuredClone(catalog); altered.locales.de.messages.live = 'Model {wrong}';
  assert.throws(() => validateCatalog(altered));
  assert.equal(formatMessage('{model}: {count}', {model:'actual-model',count:3}), 'actual-model: 3');
});

test('builder uses advertised languages, preserves placeholders, and refuses provider failure without output', async () => {
  const directory = await mkdtemp(join(tmpdir(),'spp-i18n-test-'));
  let unavailable = true;
  const server = createServer(async (req,res) => {
    res.setHeader('Content-Type','application/json');
    if (req.url === '/api/v1/languages') {res.end(JSON.stringify({languages:{en:{name:'English'},fr:{name:'French'}}}));return;}
    if (unavailable) {res.statusCode=503;res.end('{}');return;}
    let body='';for await(const part of req)body+=part;
    const input=JSON.parse(body);
    res.end(JSON.stringify({source_lang:input.source,target_lang:input.target,translated:'[TEST FIXTURE ONLY] '+input.text,method:'test-fixture'}));
  });
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  try {
    const file=join(directory,'source.json'), output=join(directory,'result.json');
    await writeFile(file,JSON.stringify({defaultLocale:'en',defaultProfile:'/',responseLanguages:{en:{name:'English'}},locales:{en:{label:'English',direction:'ltr',messages:{live:'Model {model}'},profiles:{'/':{role:'Help',asks:['Question'],links:[['/account','Account']]}}}}}));
    const endpoint='http://127.0.0.1:'+server.address().port;
    await assert.rejects(buildCatalog(endpoint,output,file),/HTTP 503/);
    await assert.rejects(access(output));
    unavailable=false;
    const result=await buildCatalog(endpoint,output,file);
    assert.deepEqual(Object.keys(result.locales),['en','fr']);
    assert.equal(result.locales.fr.messages.live,'[TEST FIXTURE ONLY] Model {model}');
    await assert.rejects(buildCatalog(endpoint,output,file),/already exists/);
  } finally {server.closeAllConnections();server.close();await rm(directory,{recursive:true,force:true});}
});
