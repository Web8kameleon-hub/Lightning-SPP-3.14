// Local acceptance against the real API and isolated fixture account seeded by WorkflowApiTests.
import {createServer} from 'node:http';
import {spawn} from 'node:child_process';
import {mkdir, writeFile, readFile} from 'node:fs/promises';
const backend = process.env.SPP_WORKFLOW_BROWSER_BACKEND;
if (!backend || !['localhost','127.0.0.1'].includes(new URL(backend).hostname)) throw new Error('Isolated local test backend required');
const checks = [];
let complete;
const done = new Promise(resolve => {complete = resolve;});
const driver = `
(async () => {
const check = (value, text) => { if (!value) throw new Error(text); };
const el = id => document.getElementById(id);
const wait = async condition => { const end = Date.now()+45000; while (!condition()) { if(Date.now()>end) throw new Error('Timed out waiting for UI'); await new Promise(r=>setTimeout(r,80)); } };
try {
 if (!sessionStorage.getItem('workflow-acceptance-login')) {
   const csrf = (await (await fetch('/api/account/csrf')).json()).csrfToken;
   const login = await fetch('/api/account/login',{method:'POST',headers:{'Content-Type':'application/json','X-CSRF-Token':csrf},body:JSON.stringify({email:'workspace-fixture@example.test',password:'test-only-workspace-password-123!'})});
   check(login.ok,'Real fixture login failed'); sessionStorage.setItem('workflow-acceptance-login','1'); location.reload(); return;
 }
 await wait(()=>!el('workspaceBody').hidden);
 const dt = new DataTransfer(); dt.items.add(new File(['actual browser-imported text'],'browser-source.txt',{type:'text/plain'})); el('workspaceUpload').files=dt.files;
 el('importFiles').click(); await wait(()=>el('workspaceFiles').textContent.includes('browser-source.txt')&&!el('importFiles').disabled);
 el('workflowGoal').value='Read the selected original'; el('registeredTool').value='files.read'; el('registeredTool').dispatchEvent(new Event('change')); el('addTool').click();
 const plan=JSON.parse(el('workflowPlan').value); check(plan.steps[0].arguments.path.includes('browser-source.txt'),'Selected source not used');
 el('savePlan').click(); await wait(()=>!el('runPlan').disabled); check(el('jobProgress').textContent.includes('READY'),'Saving plan executed it');
 el('runPlan').click(); await wait(()=>el('jobProgress').textContent.includes('SUCCEEDED'));
 const result=JSON.parse(el('jobResults').textContent); check(result.steps[0].result.data.text==='actual browser-imported text','Browser did not show actual tool output');
 check(result.steps[0].result.sources[0].sha256.length===64,'Source hash absent');
 check(!el('allowModel').checked&&!el('includeScreen').checked,'Model or screen consent enabled without user action');
 await fetch('/__acceptance/result',{method:'POST',body:JSON.stringify({passed:true,checks:['real customer login','scoped original import','plan review before execution','real file execution','source hash rendering','model and screen consent default off']})});
} catch(error) {await fetch('/__acceptance/result',{method:'POST',body:JSON.stringify({passed:false,error:error.message})});}
})();`;
const server = createServer(async (req,res)=>{
 try {
  if(req.url==='/__acceptance/driver.js'){res.setHeader('Content-Type','text/javascript');res.end(driver);return;}
  if(req.url==='/__acceptance/result'){let data='';for await(const part of req)data+=part;const result=JSON.parse(data);checks.push(result);res.end('ok');complete(result);return;}
  const chunks=[];for await(const part of req)chunks.push(part);
  const headers={...req.headers}; delete headers.host; delete headers.connection; delete headers['content-length'];
  if(headers.origin)headers.origin=new URL(backend).origin;
  headers['x-forwarded-for']='192.0.2.246';
  const upstream=await fetch(new URL(req.url,backend),{method:req.method,headers,...(['GET','HEAD'].includes(req.method)?{}:{body:Buffer.concat(chunks)})});
  res.statusCode=upstream.status; for(const [key,value] of upstream.headers)if(!['content-length','content-encoding','transfer-encoding','set-cookie'].includes(key))res.setHeader(key,value);
  if(upstream.headers.getSetCookie().length)res.setHeader('set-cookie',upstream.headers.getSetCookie());
  let body=Buffer.from(await upstream.arrayBuffer());
  if(req.url==='/workspace')body=Buffer.from(body.toString().replace('</head>','<script src="/__acceptance/driver.js" defer></script></head>'));
  res.end(body);
 }catch(error){res.statusCode=500;res.end('Local acceptance proxy failed');complete({passed:false,error:error.message});}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const edge=process.env.SPP_TEST_BROWSER||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const profile=await import('node:fs/promises').then(async fs=>fs.mkdtemp((await import('node:os')).tmpdir()+'/lightning-workflow-browser-'));
const browser=spawn(edge,['--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--window-size=1440,1000','--remote-debugging-port=0','--user-data-dir='+profile,'http://localhost:'+server.address().port+'/workspace'],{windowsHide:true,stdio:'ignore'});
browser.on('error',error=>complete({passed:false,error:error.message}));
const timeout=setTimeout(()=>complete({passed:false,error:'Browser acceptance timeout'}),65000);
try {
 const result=await done;await mkdir('artifacts/workflow-current',{recursive:true});
 if(result.passed){
  const port=(await readFile(profile+'/DevToolsActivePort','utf8')).split('\n')[0];
  const tabs=await(await fetch('http://127.0.0.1:'+port+'/json/list')).json();
  const socket=new WebSocket(tabs.find(tab=>tab.type==='page').webSocketDebuggerUrl);
  await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
  let seq=0;const pending=new Map();socket.addEventListener('message',event=>{const message=JSON.parse(event.data);if(message.id&&pending.has(message.id)){const handlers=pending.get(message.id);pending.delete(message.id);message.error?handlers.reject(new Error(message.error.message)):handlers.resolve(message.result);}});
  const command=(method,params={})=>new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params}));});
  const metrics=await command('Page.getLayoutMetrics');const size=metrics.cssContentSize||metrics.contentSize;
  const screenshot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:true,clip:{x:0,y:0,width:size.width,height:size.height,scale:1}});
  await writeFile('artifacts/workflow-current/workspace-browser.png',Buffer.from(screenshot.data,'base64'));result.screenshot='workspace-browser.png';
  await command('Browser.close');socket.close();
 }
 await writeFile('artifacts/workflow-current/browser-acceptance.json',JSON.stringify(result,null,2));
 console.log(JSON.stringify(result));process.exitCode=result.passed?0:1;
}finally{clearTimeout(timeout);browser.kill();server.closeAllConnections();server.close();}
