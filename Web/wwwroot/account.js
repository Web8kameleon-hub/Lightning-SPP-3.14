'use strict';
const byId = id => document.getElementById(id);
let csrf;
let linkToken;
function message(text, error = false) { byId('accountMessage').textContent = text; byId('accountMessage').className = 'message ' + (error ? 'error' : ''); }
async function request(action, data) {
  let response;
  try { response = await fetch('/api/account' + action, data ? {
    method: 'POST', headers: {'Content-Type': 'application/json', 'X-CSRF-Token': csrf},
    body: JSON.stringify(data), signal: AbortSignal.timeout(60000)
  } : {signal: AbortSignal.timeout(10000)}); }
  catch (error) {
    if (error.name === 'TimeoutError' || error.name === 'AbortError')
      throw new Error('The request timed out. An account or email request may already have been saved. Use Account recovery or refresh before retrying; do not pay again.');
    throw error;
  }
  const body = await response.json();
  if (!response.ok) throw Object.assign(new Error(body.error || body.message || 'Account request failed.'), {status:response.status, code:body.code});
  return body;
}
async function refresh() {
  csrf = (await request('/csrf')).csrfToken;
  let account;
  try { account = await request(''); }
  catch (error) {
    byId('customer').hidden = true; byId('anonymous').hidden = false;
    byId('accountState').hidden = true; byId('accountState').textContent = '';
    byId('entitlements').replaceChildren();
    if (error.status !== 401) throw error;
    return;
  }
  csrf = account.csrfToken;
  byId('customer').hidden = false; byId('anonymous').hidden = true;
  byId('accountEmail').textContent = account.email + ' · email verified';
  byId('accountState').textContent = 'Account: VERIFIED'; byId('accountState').hidden = false;
  byId('emptyEntitlements').hidden = account.entitlements.length !== 0;
  byId('entitlements').replaceChildren();
  for (const entitlement of account.entitlements) {
    const card = document.createElement('article'); card.className = 'card';
    const title = document.createElement('h3'); title.textContent = entitlement.tier;
    const status = document.createElement('p'); status.textContent = 'License: ' + (entitlement.licenseState || entitlement.state) + ' · subscription: ' + entitlement.subscriptionStatus;
    const activation = document.createElement('p'); activation.textContent = entitlement.activationRecorded ? 'This account has a registered browser.' : 'Browser registration is optional here. Your deployment administrator configures the licensed service; downloading a runtime alone does not activate it.';
    const expiry = document.createElement('p'); expiry.textContent = 'Paid period ends: ' + new Date(entitlement.expires * 1000).toISOString().slice(0,10);
    const email = document.createElement('p'); email.textContent = entitlement.emailState === 'PROVIDER_ACCEPTED' ? 'Notification accepted by the email provider; inbox arrival is not confirmed.' : 'Your existing license is stored here. Use Show license to retrieve it; Retry notification sends the same key.';
    const key = document.createElement('input'); key.readOnly = true; key.setAttribute('aria-label','Your signed license key'); key.autocomplete = 'off';
    card.append(title,status,activation,expiry,email,key);
    for (const [label, action] of [['Show license','license'], ['Register this browser','activate'], ['Resend existing license','retry-email']]) {
      const button = document.createElement('button'); button.type = 'button'; button.textContent = label;
      button.addEventListener('click', () => busy(button, async () => {
        const payload = {entitlementId:entitlement.id};
        if (action === 'activate') {
          const name = 'lightning-device-' + account.userId;
          const deviceId = localStorage.getItem(name) || crypto.randomUUID(); localStorage.setItem(name,deviceId); payload.deviceId = deviceId;
        }
        const result = await request('/' + action,payload);
        if (action === 'license') key.value = result.licenseKey;
        else { message(result.message); await refresh(); }
      }));
      card.append(button);
    }
    byId('entitlements').append(card);
  }
  byId('priceId').replaceChildren();
  for (const plan of account.plans) {
    const option = document.createElement('option'); option.value = plan.priceId; option.textContent = plan.tier + ' · ' + plan.mode; byId('priceId').append(option);
  }
  byId('checkoutForm').hidden = account.plans.length === 0 || account.canPurchase !== true;
}
async function busy(button, action) {
  button.disabled = true;
  try { await action(); } catch (error) {
    message(error.message,true);
    if (error.code === 'email_not_verified') {
      byId('accountState').textContent = 'Account: UNVERIFIED · Action required: Verify email';
      byId('accountState').hidden = false;
      byId('recoveryEmail').value = byId('loginEmail').value.trim().toLowerCase();
      byId('recoveryAction').value = 'request-verification';
      byId('loginPassword').value = '';
      byId('recoveryForm').querySelector('button').focus();
    }
  }
  finally { button.disabled = false; }
}
function form(id, action, payload, after = async result => message(result.message)) {
  byId(id).addEventListener('submit', event => {
    event.preventDefault();
    if (id === 'loginForm') { byId('accountState').hidden = true; byId('accountState').textContent = ''; }
    busy(event.currentTarget.querySelector('button'), async () => {
      const result = await request(typeof action === 'function' ? action() : action,payload());
      await after(result);
    });
  });
}
form('registerForm','/register',() => ({email:byId('registerEmail').value,password:byId('registerPassword').value}), async result => { byId('registerPassword').value=''; message(result.message); });
form('loginForm','/login',() => ({email:byId('loginEmail').value,password:byId('loginPassword').value}),async result => { byId('loginPassword').value=''; message(result.message); await refresh(); window.dispatchEvent(new Event('lightning:account-changed')); });
form('recoveryForm',() => '/' + byId('recoveryAction').value,() => ({email:byId('recoveryEmail').value}));
form('verifyForm','/verify',() => ({token:linkToken}),async result => { linkToken=undefined; byId('verifyForm').hidden=true; message(result.message); await refresh(); });
form('resetForm','/reset',() => ({token:linkToken,password:byId('resetPassword').value}),async result => { linkToken=undefined; byId('resetPassword').value=''; byId('resetForm').hidden=true; message(result.message); await refresh(); });
function redirect(result, host) {
  const url = new URL(result.url); if (url.protocol !== 'https:' || url.hostname !== host) throw new Error('Unexpected billing destination.'); location.assign(url.href);
}
form('checkoutForm','/checkout',() => ({priceId:byId('priceId').value}),async result => redirect(result,'checkout.stripe.com'));
byId('billing').addEventListener('click',event => busy(event.currentTarget,async () => redirect(await request('/billing',{}),'billing.stripe.com')));
byId('logout').addEventListener('click',event => busy(event.currentTarget,async () => { await request('/logout',{}); await refresh(); window.dispatchEvent(new Event('lightning:account-changed')); message('Signed out.'); }));
byId('refreshAccount').addEventListener('click',event => busy(event.currentTarget,refresh));
const fragment = new URLSearchParams(location.hash.slice(1));
for (const purpose of ['verify','reset']) if (fragment.has(purpose)) {
  linkToken = fragment.get(purpose); byId(purpose + 'Form').hidden=false;
  history.replaceState(null,'',location.pathname);
}
if (new URLSearchParams(location.search).get('purchase') === 'success') message('Checkout returned you here. Refresh your account to check the actual entitlement state. If you already paid, do not pay again.');
refresh().catch(error => message(error.message,true));
