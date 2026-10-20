// Real browser against isolated HTTP fixtures. No fixture response is production evidence.
import {createServer} from 'node:http';
import {spawn} from 'node:child_process';
import {readFile,mkdtemp} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
let complete; const result=new Promise(resolve=>{complete=resolve;});
const driver=`(async()=>{
const check=(v,m)=>{if(!v)throw new Error(m);};
const wait=async f=>{const end=Date.now()+12000;while(!f()){if(Date.now()>end)throw new Error('UI timeout');await new Promise(r=>setTimeout(r,25));}};
try{
 await wait(()=>document.querySelector('.lspp-launch'));
 document.querySelector('.lspp-launch').click();
 await wait(()=>document.querySelector('.lspp-assist').dataset.mode==='live');
 const catalog=await fetch('/assistant-locales.json').then(r=>r.json());
 check(document.querySelectorAll('.lspp-language')[1].options.length===Object.keys(catalog.responseLanguages).length+1,'Language choices missing');
 const language=document.querySelector('.lspp-language');
 for(const locale of Object.keys(catalog.locales)){
  language.value=locale;language.dispatchEvent(new Event('change'));
  await wait(()=>document.querySelector('.lspp-assist').dataset.mode==='live');
  window.dispatchEvent(new CustomEvent('lightning:pdf-result',{detail:{pages:2,originalBytes:1024,afterBytes:2048,totalTimeMs:1250}}));
  document.querySelector('.lspp-chips button').click();
  const message=document.querySelector('.lspp-log').lastElementChild;
  check(message.querySelector('a')?.getAttribute('href')==='/pdf#results','PDF measurement source missing for '+locale);
  check(message.textContent.includes('2')&&message.textContent.includes('+100'),'Actual PDF measurements missing');
 }
 window.dispatchEvent(new CustomEvent('lightning:pdf-result',{detail:null}));
 document.querySelector('.lspp-chips button').click();
 await wait(()=>!document.querySelector('.lspp-assist').dataset.busy);
 const request=await fetch('/fixture-request').then(r=>r.json());
 check(request.automaticQualityRouting===true&&!('route' in request),'Assistant still picks first route');
 check(document.querySelector('.lspp-log').lastElementChild.textContent.includes('fixture-selected-model'),'Actual model identity missing');
 await fetch('/fixture-result',{method:'POST',body:JSON.stringify({passed:true,checks:['all catalog choices','PDF measurements in every shipped interface','PDF source clearing','automatic route request','actual selected model shown']})});
}catch(e){await fetch('/fixture-result',{method:'POST',body:JSON.stringify({passed:false,error:e.message})});}
})();`;
let request;
const server=createServer(async(req,res)=>{
 try{
  res.setHeader('Content-Type','application/json');
  if(req.url==='/pdf'){res.setHeader('Content-Type','text/html');res.end('<!doctype html><html><head><title>Isolated browser fixture</title><script src="/assistant.js" defer></script><script src="/fixture-driver.js" defer></script></head><body><main><h1>Isolated PDF fixture</h1></main></body></html>');return;}
  if(req.url==='/fixture-driver.js'){res.setHeader('Content-Type','text/javascript');res.end(driver);return;}
  if(req.url==='/api/workflow'){res.end(JSON.stringify({csrfToken:'test-only',automaticQualityRouting:true,models:[{id:'first-fixture',model:'not-selected'},{id:'second-fixture',model:'fixture-selected-model'}]}));return;}
  if(req.url==='/api/workflow/conversation'){let body='';for await(const part of req)body+=part;request=JSON.parse(body);res.end(JSON.stringify({text:'TEST FIXTURE ONLY',identity:{model:'fixture-selected-model'},quality:{scope:'translation-reference-fidelity'}}));return;}
  if(req.url==='/fixture-request'){res.end(JSON.stringify(request));return;}
  if(req.url==='/fixture-result'){let body='';for await(const part of req)body+=part;complete(JSON.parse(body));res.end('{}');return;}
  const files={'/assistant.js':'text/javascript','/assistant-i18n.mjs':'text/javascript','/assistant-locales.json':'application/json','/assistant.css':'text/css'};
  if(files[req.url]){res.setHeader('Content-Type',files[req.url]);res.end(await readFile(new URL('../Web/wwwroot'+req.url,import.meta.url)));return;}
  res.statusCode=404;res.end('{}');
 }catch(e){res.statusCode=500;res.end('{}');complete({passed:false,error:e.message});}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const profile=await mkdtemp(join(tmpdir(),'spp-assistant-fixture-'));
const browser=spawn(process.env.SPP_TEST_BROWSER||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
 ['--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--user-data-dir='+profile,'http://127.0.0.1:'+server.address().port+'/pdf'],{windowsHide:true,stdio:'ignore'});
browser.on('error',e=>complete({passed:false,error:e.message}));
const timeout=setTimeout(()=>complete({passed:false,error:'Browser fixture timeout'}),45000);
try{const outcome=await result;console.log(JSON.stringify(outcome));process.exitCode=outcome.passed?0:1;}
finally{clearTimeout(timeout);browser.kill();server.closeAllConnections();server.close();}
