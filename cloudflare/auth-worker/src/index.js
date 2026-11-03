const json = (data, status = 200) => new Response(JSON.stringify(data), {
  status, headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store", "access-control-allow-origin": "*", "access-control-allow-methods": "GET,POST,OPTIONS", "access-control-allow-headers": "content-type,authorization" }
});
const now = () => Math.floor(Date.now() / 1000);
const enc = new TextEncoder();
const hex = b => [...new Uint8Array(b)].map(x => x.toString(16).padStart(2,"0")).join("");
const random = (n=32) => { const b = crypto.getRandomValues(new Uint8Array(n)); return [...b].map(x=>x.toString(16).padStart(2,"0")).join(""); };
async function digest(s) { return hex(await crypto.subtle.digest("SHA-256", enc.encode(s))); }
async function passwordHash(password, salt = random(16)) {
  const key = await crypto.subtle.importKey("raw", enc.encode(password), "PBKDF2", false, ["deriveBits"]);
  const bits = await crypto.subtle.deriveBits({name:"PBKDF2",salt:enc.encode(salt),iterations:310000,hash:"SHA-256"},key,256);
  return `pbkdf2-sha256$310000$${salt}$${hex(bits)}`;
}
async function verifyPassword(password, stored) {
  const [scheme, iterations, salt, expected] = stored.split("$");
  if (scheme !== "pbkdf2-sha256" || Number(iterations)!==310000) return false;
  const actual = (await passwordHash(password,salt)).split("$")[3];
  return actual.length===expected.length && [...actual].reduce((n,c,i)=>n | (c.charCodeAt(0)^expected.charCodeAt(i)),0)===0;
}
async function secretHash(value, env) { return digest(`${env.CODE_PEPPER}:${value}`); }
function constantTimeEqual(a,b) { if(typeof a!=="string"||typeof b!=="string"||a.length!==b.length) return false; let diff=0; for(let i=0;i<a.length;i++) diff|=a.charCodeAt(i)^b.charCodeAt(i); return diff===0; }
async function rateLimit(env,key,max,seconds) {
  const t=now(), start=t-(t%seconds);
  const row=await env.DB.prepare("SELECT window_start,hits FROM rate_limits WHERE bucket_key=?").bind(key).first();
  if(!row || row.window_start!==start) await env.DB.prepare("INSERT INTO rate_limits(bucket_key,window_start,hits) VALUES(?,?,1) ON CONFLICT(bucket_key) DO UPDATE SET window_start=excluded.window_start,hits=1").bind(key,start).run();
  else if(row.hits>=max) return false;
  else await env.DB.prepare("UPDATE rate_limits SET hits=hits+1 WHERE bucket_key=?").bind(key).run();
  return true;
}
async function sendCode(env,email,code,purpose) {
  const subject=purpose==="verify" ? "Verify your RSS Label Maker account" : "Reset your RSS Label Maker password";
  const html=`<div style="font-family:Arial,sans-serif;max-width:520px;margin:auto;color:#20242b"><h2>RSS Label Maker</h2><p>${purpose==="verify"?"Use this code to verify your email address:":"Use this code to reset your password:"}</p><p style="font-size:30px;font-weight:bold;letter-spacing:8px">${code}</p><p>This code expires in 10 minutes. If you did not request this, you can ignore this email.</p><p>Razeen Secure Solution</p></div>`;
  const response=await fetch("https://api.resend.com/emails",{method:"POST",headers:{authorization:`Bearer ${env.RESEND_API_KEY}`,"content-type":"application/json"},body:JSON.stringify({from:env.MAIL_FROM,to:[email],subject,html})});
  if(!response.ok) throw new Error("Email delivery service rejected the message");
}
async function issueChallenge(env,account,email,purpose) {
  const t=now(), recent=await env.DB.prepare("SELECT created_at FROM email_challenges WHERE email=? AND purpose=? ORDER BY created_at DESC LIMIT 1").bind(email,purpose).first();
  if(recent && t-recent.created_at<60) return false;
  const code=String(crypto.getRandomValues(new Uint32Array(1))[0]%1000000).padStart(6,"0");
  const id=random(16), hash=await secretHash(code,env);
  await env.DB.prepare("INSERT INTO email_challenges(id,account_id,email,purpose,code_hash,expires_at,created_at) VALUES(?,?,?,?,?,?,?)").bind(id,account?.id??null,email,purpose,hash,t+600,t).run();
  await sendCode(env,email,code,purpose);
  return true;
}
async function createSession(env,account,deviceName) {
  const token=random(32), t=now(), id=random(16);
  await env.DB.prepare("INSERT INTO device_sessions(id,account_id,token_hash,device_name,created_at,expires_at) VALUES(?,?,?,?,?,?)").bind(id,account.id,await secretHash(token,env),deviceName.slice(0,100),t,t+31536000).run();
  return {token,account:{id:account.id,fullName:account.full_name,email:account.email},expiresAt:t+31536000};
}
async function authSession(request,env) {
  const token=(request.headers.get("authorization")||"").replace(/^Bearer\s+/i,"");
  if(!token) return null;
  return env.DB.prepare("SELECT s.id session_id,s.account_id,a.full_name,a.email FROM device_sessions s JOIN accounts a ON a.id=s.account_id WHERE s.token_hash=? AND s.revoked_at IS NULL AND s.expires_at>? AND a.verified_at IS NOT NULL").bind(await secretHash(token,env),now()).first();
}
export default {
 async fetch(request,env) {
  if(request.method==="OPTIONS") return new Response(null,{status:204,headers:{"access-control-allow-origin":"*","access-control-allow-methods":"GET,POST,OPTIONS","access-control-allow-headers":"content-type,authorization"}});
  const url=new URL(request.url), path=url.pathname.replace(/\/$/,"")||"/";
  if(request.method==="GET" && path==="/health") return json({ok:true,service:"rss-label-maker-auth"});
  if(request.method!=="POST") return json({error:"Not found"},404);
  let body; try { body=await request.json(); } catch { return json({error:"Invalid request"},400); }
  const ip=request.headers.get("cf-connecting-ip")||"unknown";
  try {
   if(path==="/auth/register") {
    const fullName=String(body.fullName||"").trim(), email=String(body.email||"").trim().toLowerCase(), password=String(body.password||"");
    if(fullName.length<2||fullName.length>100||!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)||email.length>254||password.length<12||password.length>128) return json({error:"Enter a valid name, email and password (12–128 characters)."},400);
    if(!await rateLimit(env,`register:${ip}`,8,3600)||!await rateLimit(env,`register-email:${email}`,3,3600)) return json({error:"Too many attempts. Try again later."},429);
    let account=await env.DB.prepare("SELECT * FROM accounts WHERE email=?").bind(email).first();
    if(account?.verified_at) return json({message:"If registration can proceed, a verification message will be sent. Try signing in if you already verified."},202);
    const t=now(), id=account?.id||random(16), hash=await passwordHash(password);
    if(account) await env.DB.prepare("UPDATE accounts SET full_name=?,password_hash=?,updated_at=? WHERE id=?").bind(fullName,hash,t,id).run();
    else await env.DB.prepare("INSERT INTO accounts(id,full_name,email,password_hash,created_at,updated_at) VALUES(?,?,?,?,?,?)").bind(id,fullName,email,hash,t,t).run();
    await issueChallenge(env,{id},email,"verify");
    return json({message:"If the address is eligible, a verification code has been sent."},202);
   }
   if(path==="/auth/verify") {
    const email=String(body.email||"").trim().toLowerCase(), code=String(body.code||"").trim();
    if(!/^[0-9]{6}$/.test(code)||!await rateLimit(env,`verify:${ip}:${email}`,12,900)) return json({error:"Invalid or expired verification code."},400);
    const challenge=await env.DB.prepare("SELECT * FROM email_challenges WHERE email=? AND purpose='verify' AND consumed_at IS NULL ORDER BY created_at DESC LIMIT 1").bind(email).first();
    if(!challenge||challenge.expires_at<now()||challenge.attempts>=5) return json({error:"Invalid or expired verification code."},400);
    const valid=constantTimeEqual(await secretHash(code,env),challenge.code_hash);
    await env.DB.prepare("UPDATE email_challenges SET attempts=attempts+1,consumed_at=CASE WHEN ? THEN ? ELSE consumed_at END WHERE id=?").bind(valid?1:0,now(),challenge.id).run();
    if(!valid) return json({error:"Invalid or expired verification code."},400);
    const account=await env.DB.prepare("SELECT * FROM accounts WHERE email=?").bind(email).first();
    if(!account) return json({error:"Invalid or expired verification code."},400);
    await env.DB.prepare("UPDATE accounts SET verified_at=?,updated_at=? WHERE id=?").bind(now(),now(),account.id).run();
    await env.DB.prepare("UPDATE email_challenges SET consumed_at=? WHERE email=? AND purpose='verify' AND consumed_at IS NULL").bind(now(),email).run();
    return json(await createSession(env,account,String(body.deviceName||"Windows PC")));
   }
   if(path==="/auth/resend-verification") {
    const email=String(body.email||"").trim().toLowerCase();
    if(!await rateLimit(env,`resend:${ip}`,10,3600)) return json({error:"Too many requests."},429);
    const account=await env.DB.prepare("SELECT id FROM accounts WHERE email=? AND verified_at IS NULL").bind(email).first();
    if(account) await issueChallenge(env,account,email,"verify");
    return json({message:"If verification is pending, a new code will be sent."},202);
   }
   if(path==="/auth/login") {
    const email=String(body.email||"").trim().toLowerCase(), password=String(body.password||"");
    if(!await rateLimit(env,`login:${ip}:${email}`,10,900)) return json({error:"Too many attempts. Try again later."},429);
    const account=await env.DB.prepare("SELECT * FROM accounts WHERE email=?").bind(email).first();
    if(!account||!await verifyPassword(password,account.password_hash)) return json({error:"Email or password is incorrect."},401);
    if(!account.verified_at) return json({error:"Email verification is required. Check your inbox."},403);
    return json(await createSession(env,account,String(body.deviceName||"Windows PC")));
   }
   if(path==="/auth/session") {
    const session=await authSession(request,env);
    return session?json({valid:true,account:{id:session.account_id,fullName:session.full_name,email:session.email}}):json({valid:false},401);
   }
   if(path==="/auth/logout") {
    const token=(request.headers.get("authorization")||"").replace(/^Bearer\s+/i,"");
    if(token) await env.DB.prepare("UPDATE device_sessions SET revoked_at=? WHERE token_hash=? AND revoked_at IS NULL").bind(now(),await secretHash(token,env)).run();
    return json({ok:true});
   }
   if(path==="/auth/forgot-password") {
    const email=String(body.email||"").trim().toLowerCase();
    if(!await rateLimit(env,`reset:${ip}`,5,3600)) return json({message:"If the account exists, a reset code will be sent."},202);
    const account=await env.DB.prepare("SELECT * FROM accounts WHERE email=? AND verified_at IS NOT NULL").bind(email).first();
    if(account) await issueChallenge(env,account,email,"reset");
    return json({message:"If the account exists, a reset code will be sent."},202);
   }
   if(path==="/auth/reset-password") {
    const email=String(body.email||"").trim().toLowerCase(),code=String(body.code||""),password=String(body.password||"");
    if(!/^[0-9]{6}$/.test(code)||password.length<12||password.length>128||!await rateLimit(env,`reset-code:${ip}:${email}`,10,900)) return json({error:"Invalid request."},400);
    const challenge=await env.DB.prepare("SELECT * FROM email_challenges WHERE email=? AND purpose='reset' AND consumed_at IS NULL ORDER BY created_at DESC LIMIT 1").bind(email).first();
    if(!challenge||challenge.expires_at<now()||challenge.attempts>=5||!constantTimeEqual(await secretHash(code,env),challenge.code_hash)) {
      if(challenge) await env.DB.prepare("UPDATE email_challenges SET attempts=attempts+1 WHERE id=?").bind(challenge.id).run();
      return json({error:"Invalid or expired reset code."},400);
    }
    const account=await env.DB.prepare("SELECT id FROM accounts WHERE email=? AND verified_at IS NOT NULL").bind(email).first();
    if(!account) return json({error:"Invalid or expired reset code."},400);
    await env.DB.prepare("UPDATE email_challenges SET consumed_at=? WHERE id=?").bind(now(),challenge.id).run();
    await env.DB.prepare("UPDATE accounts SET password_hash=?,updated_at=? WHERE id=?").bind(await passwordHash(password),now(),account.id).run();
    await env.DB.prepare("UPDATE device_sessions SET revoked_at=? WHERE account_id=? AND revoked_at IS NULL").bind(now(),account.id).run();
    return json({message:"Password reset. Sign in again on this device."});
   }
   return json({error:"Not found"},404);
  } catch(error) {
   console.error("Auth request failed", {path, message:String(error?.message||"unknown")});
   return json({error:"The authentication service could not complete this request. Please try again."},503);
  }
 }
};