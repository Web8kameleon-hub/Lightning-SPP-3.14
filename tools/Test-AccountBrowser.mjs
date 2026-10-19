// Local headless browser acceptance against real API/Longo fixtures from AccountFlowTests.
import {createServer} from 'node:http';
import {spawn} from 'node:child_process';
import {mkdir, writeFile, readFile} from 'node:fs/promises';
const backend = process.env.SPP_ACCOUNT_BROWSER_BACKEND;
if (!backend || !['localhost','127.0.0.1'].includes(new URL(backend).hostname)) throw new Error('Isolated local test backend required');
const checks = [];
let complete;
const done = new Promise(resolve => {complete = resolve;});
const driver = `
(async () => {
const check=(value,text)=>{if(!value)throw new Error(text);};
const el=id=>document.getElementById(id);
const wait=async condition=>{const end=Date.now()+20000;while(!condition()){if(Date.now()>end)throw new Error('Account UI timeout');await new Promise(r=>setTimeout(r,60));}};
const login=async(email,password)=>{el('loginEmail').value=email;el('loginPassword').value=password;el('loginForm').dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}));await wait(()=>!el('loginForm').querySelector('button').disabled);};
try {
 await wait(()=>typeof csrf==='string');
 document.querySelector('.lspp-launch').click();
 await wait(()=>document.querySelector('.lspp-assist').dataset.mode==='signin');
 await login('ANOTHER@DIFFERENT.EXAMPLE','test-only-starting-password-123!');
 check(el('accountState').textContent.includes('UNVERIFIED'),'Unverified account status missing');
 check(el('accountMessage').textContent.includes('not verified'),'Verification action missing');
 check(el('recoveryEmail').value==='another@different.example','Recovery email not canonical');
 check(el('recoveryAction').value==='request-verification','Wrong recovery action');
 check(el('customer').hidden,'Unverified account received customer view');
 await login('another@different.example','wrong-test-password');
 check(el('accountMessage').textContent.includes('incorrect'),'Wrong-password error missing');
 check(el('accountState').hidden,'Stale account status survived another login attempt');
 await login('BUYER+TRIAL@EXAMPLE.TEST','test-only-replacement-password-456!');
 await wait(()=>!el('customer').hidden);
 check(el('accountState').textContent==='Account: VERIFIED','Verified state missing');
 await wait(()=>['live','nomodel'].includes(document.querySelector('.lspp-assist').dataset.mode));
 check(!document.querySelector('.lspp-state').textContent.includes('Sign in'),'Assistant retained stale sign-in state');
 check(el('entitlements').textContent.includes('License: ACTIVE'),'Valid paid key not shown as active');
 check(el('entitlements').textContent.includes('Browser registration is optional'),'Browser and license state still coupled');
 check(el('loginPassword').value==='','Login password retained in form after login');
 el('logout').click();await wait(()=>!el('logout').disabled&&!el('anonymous').hidden);
 check(el('accountState').hidden,'Verified status remained visible after logout');
 await wait(()=>document.querySelector('.lspp-assist').dataset.mode==='signin');
 await fetch('/__acceptance/result',{method:'POST',body:JSON.stringify({passed:true,checks:['correct-password unverified state','wrong-password privacy','canonical recovery address','verified login','active license independent of browser registration','password form cleared','logout clears account status','open assistant refreshes after login and logout']})});
} catch(error){await fetch('/__acceptance/result',{method:'POST',body:JSON.stringify({passed:false,error:error.message})});}
})();`;
const server = createServer(async (req,res)=>{
 try {
  if(req.url==='/__acceptance/driver.js'){res.setHeader('Content-Type','text/javascript');res.end(driver);return;}
  if(req.url==='/__acceptance/result'){let data='';for await(const part of req)data+=part;const result=JSON.parse(data);checks.push(result);res.end('ok');complete(result);return;}
  const chunks=[];for await(const part of req)chunks.push(part);
  const headers={...req.headers}; delete headers.host; delete headers.connection; delete headers['content-length'];
  if(headers.origin)headers.origin=new URL(backend).origin;
  headers['x-forwarded-for']='192.0.2.248';
  const upstream=await fetch(new URL(req.url,backend),{method:req.method,headers,...(['GET','HEAD'].includes(req.method)?{}:{body:Buffer.concat(chunks)})});
  res.statusCode=upstream.status; for(const [key,value] of upstream.headers)if(!['content-length','content-encoding','transfer-encoding','set-cookie'].includes(key))res.setHeader(key,value);
  if(upstream.headers.getSetCookie().length)res.setHeader('set-cookie',upstream.headers.getSetCookie());
  let body=Buffer.from(await upstream.arrayBuffer());
  if(req.url==='/account')body=Buffer.from(body.toString().replace('</head>','<script src="/__acceptance/driver.js" defer></script></head>'));
  res.end(body);
 }catch(error){res.statusCode=500;res.end('Local acceptance proxy failed');complete({passed:false,error:error.message});}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const edge=process.env.SPP_TEST_BROWSER||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const profile=await import('node:fs/promises').then(async fs=>fs.mkdtemp((await import('node:os')).tmpdir()+'/lightning-account-browser-'));
const browser=spawn(edge,['--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--window-size=1440,1000','--remote-debugging-port=0','--user-data-dir='+profile,'http://localhost:'+server.address().port+'/account'],{windowsHide:true,stdio:'ignore'});
browser.on('error',error=>complete({passed:false,error:error.message}));
const timeout=setTimeout(()=>complete({passed:false,error:'Browser acceptance timeout'}),65000);
try {
 const result=await done;await mkdir('artifacts/account-current',{recursive:true});
 if(result.passed){
  const port=(await readFile(profile+'/DevToolsActivePort','utf8')).split('\n')[0];
  const tabs=await(await fetch('http://127.0.0.1:'+port+'/json/list')).json();
  const socket=new WebSocket(tabs.find(tab=>tab.type==='page').webSocketDebuggerUrl);
  await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
  let seq=0;const pending=new Map();socket.addEventListener('message',event=>{const message=JSON.parse(event.data);if(message.id&&pending.has(message.id)){const handlers=pending.get(message.id);pending.delete(message.id);message.error?handlers.reject(new Error(message.error.message)):handlers.resolve(message.result);}});
  const command=(method,params={})=>new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params}));});
  const metrics=await command('Page.getLayoutMetrics');const size=metrics.cssContentSize||metrics.contentSize;
  const screenshot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:true,clip:{x:0,y:0,width:size.width,height:size.height,scale:1}});
  await writeFile('artifacts/account-current/account-browser.png',Buffer.from(screenshot.data,'base64'));result.screenshot='account-browser.png';
  await command('Browser.close');socket.close();
 }
 await writeFile('artifacts/account-current/browser-acceptance.json',JSON.stringify(result,null,2));
 console.log(JSON.stringify(result));process.exitCode=result.passed?0:1;
}finally{clearTimeout(timeout);browser.kill();server.closeAllConnections();server.close();}
